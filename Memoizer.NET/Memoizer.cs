﻿/*
 * Copyright 2011 the original author or authors.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Memoizer.NET
{

    #region IThreadSafe
    /// <summary>
    /// Marker interface for thread-safe classes.
    /// </summary>
    public interface IThreadSafe { }
    #endregion

    #region IClearable
    /// <summary>
    /// Interface for classes that can be cleared.
    /// </summary>
    public interface IClearable
    {
        void Clear();
    }
    #endregion

    #region IMemoizer
    public interface IManageableMemoizer : IThreadSafe, IDisposable, IClearable
    {
        int NumberOfTimesInvoked { get; }
        int NumberOfTimesNoCacheInvoked { get; }
        int NumberOfTimesCleared { get; }
        int NumberOfElementsCleared { get; }
    }
    public interface IMemoizer<in TParam1, out TResult> : IManageableMemoizer
    {
        TResult InvokeWith(TParam1 param);
    }
    public interface IMemoizer<in TParam1, in TParam2, out TResult> : IManageableMemoizer
    {
        TResult InvokeWith(TParam1 param1, TParam2 param2);
    }
    public interface IMemoizer<in TParam1, in TParam2, in TParam3, out TResult> : IManageableMemoizer
    {
        TResult InvokeWith(TParam1 param1, TParam2 param2, TParam3 param3);
    }
    public interface IMemoizer<in TParam1, in TParam2, in TParam3, in TParam4, out TResult> : IManageableMemoizer
    {
        TResult InvokeWith(TParam1 param1, TParam2 param2, TParam3 param3in, TParam4 param4);
    }
    #endregion

    //#region IInvocable
    ///// <summary>
    ///// Interface for invocable classes.
    ///// </summary>
    //public interface IInvocable<in TParam1, out TResult>
    //{
    //    TResult InvokeWith(TParam1 param);
    //}
    //public interface IInvocable<in TParam1, in TParam2, out TResult>
    //{
    //    TResult InvokeWith(TParam1 param1, TParam2 param2);
    //}
    //public interface IInvocable<in TParam1, in TParam2, in TParam3, out TResult>
    //{
    //    TResult InvokeWith(TParam1 param1, TParam2 param2, TParam3 param3);
    //}
    //public interface IInvocable<in TParam1, in TParam2, in TParam3, in TParam4, out TResult>
    //{
    //    TResult InvokeWith(TParam1 param1, TParam2 param2, TParam3 param3in, TParam4 param4);
    //}
    //#endregion

    #region MemoizerHelper
    // TODO: write tests and improve algorithms!!
    public class MemoizerHelper
    {
        static readonly ObjectIDGenerator OBJECT_ID_GENERATOR = new ObjectIDGenerator();

        public static string CreateParameterHash(params object[] args)
        {
            if (args.Length == 1)
            {
                if (!args[0].GetType().IsPrimitive && args[0].GetType() != typeof(String))
                {
                    bool firstTime;
                    long hash = OBJECT_ID_GENERATOR.GetId(args[0], out firstTime);
                    return hash.ToString();
                }
            }
            return string.Join(string.Empty, args);
        }
        public static string CreateMethodHash(object source) { return CreateParameterHash(source); }
        public static string CreateMethodHash(Type sourceClass, string methodName) { return CreateParameterHash(sourceClass.ToString(), methodName); }
    }
    #endregion

    #region Memoizer (using a MemoryCache instance and Goetz's algorithm)
    /// <remarks>
    /// This class is an implementation of a method-level/fine-grained cache (a.k.a. <i>memoizer</i>). 
    /// It is based on an implementation from the book "Java Concurrency in Practice" by Brian Goetz et. al. - 
    /// ported to C# 4.0 using goodness like method handles/delegates, lambda expressions, and extension methods.
    /// <p/>
    /// A <code>System.Runtime.Caching.MemoryCache</code> instance is used as cache, enabling configuration via the <code>System.Runtime.Caching.CacheItemPolicy</code>. 
    /// Default cache configuration is: items to be held as long as the CLR is alive or the memoizer is disposed/cleared.
    /// <p/>
    /// Every <code>Memoizer.Net.Memoizer</code> instance creates its own <code>System.Runtime.Caching.MemoryCache</code> instance. 
    /// One could re-design this to utilize the ubiquitous default <code>System.Runtime.Caching.MemoryCache</code> instance to make this memoizer even faster. 
    /// As a middle-way, the <code>Memoizer.Net.Memoizer</code> instance, with its <code>System.Runtime.Caching.MemoryCache</code> member instance, 
    /// can be lazy-loaded by using the <code>Memoizer.Net.LazyMemoizer</code>.
    /// <p/>
    /// This class is thread-safe.
    /// </remarks>
    /// <see>http://jcip.net/</see>
    /// <see><code>Memoizer.Net.LazyMemoizer</code></see>
    /// <author>Eirik Torske</author>
    abstract class AbstractMemoizer<TResult> : IManageableMemoizer
    {
        protected MemoryCache cache;
        protected CacheItemPolicy cacheItemPolicy;
        protected Action<string> loggingMethod;

        int numberOfTimesInvoked;
        int numberOfTimesNoCacheInvoked;
        int numberOfTimesCleared;
        int numberOfElementsCleared;

        public int NumberOfTimesInvoked { get { return this.numberOfTimesInvoked; } }
        public int NumberOfTimesNoCacheInvoked { get { return this.numberOfTimesNoCacheInvoked; } }
        public int NumberOfTimesCleared { get { return this.numberOfTimesCleared; } }
        public int NumberOfElementsCleared { get { return this.numberOfElementsCleared; } }

        /// <summary>
        /// Disposes of the memoizer.
        /// </summary>
        public void Dispose()
        {
            this.cache.Dispose();
        }

        /// <summary>
        /// Lock object for removal of element and incrementing total element removal index.
        /// </summary>
        static readonly object @lock = new Object();

        /// <summary>
        /// Clears the cache, removing all items.
        /// </summary>
        public void Clear()
        {
            lock (@lock)
            {
                int i = 0;
                foreach (var element in this.cache.AsEnumerable())
                {
                    this.cache.Remove(element.Key);
                    Interlocked.Increment(ref this.numberOfElementsCleared);
                    ConditionalLogging("Removed cached element #" + ++i + ": " + element.Key + "=" + ((Task<string>)element.Value).Status + " [" + this.NumberOfElementsCleared + " elements removed in total]");
                }
                Interlocked.Increment(ref this.numberOfTimesCleared);
                ConditionalLogging("All " + i + " elements in memoizer removed [memoizer cleared " + NumberOfTimesCleared + " times]");
            }
        }

        // TODO: try once more to put these into memoizer constructor
        internal void CacheItemPolicy(CacheItemPolicy cacheItemPolicy)
        {
            this.cacheItemPolicy = cacheItemPolicy;
        }

        // TODO: try once more to put these into memoizer constructor
        internal void InstrumentWith(Action<String> instrumenter)
        {
            this.loggingMethod = instrumenter;
        }

        protected void ConditionalLogging(string logMessage)
        {
            if (this.loggingMethod != null) { this.loggingMethod(this.GetType().Namespace + "." + this.GetType().Name + " [" + this.GetHashCode() + "] : " + logMessage); }
        }

        /// <summary>
        /// Gets the delegate of the function to be memoized, closed under given arguments.
        /// </summary>
        protected abstract Func<TResult> GetMethodClosure(params object[] args);

        /// <summary>
        /// Invokes the method delegate - consulting the cache on the way.
        /// </summary>
        protected TResult Invoke(params object[] args)
        {
            long startTime = DateTime.Now.Ticks;
            string key = MemoizerHelper.CreateParameterHash(args);
            CacheItem cacheItem = this.cache.GetCacheItem(key);
            if (cacheItem == null)
            {
                //Console.WriteLine("OS thread ID=" + AppDomain.GetCurrentThreadId() + ", " + "Managed thread ID=" + Thread.CurrentThread.GetHashCode() + "/" + Thread.CurrentThread.ManagedThreadId + ": cacheItem == null");
                //Func<TResult> func = new Func<TResult>(delegate() { return this.methodToBeMemoized((TParam)args[0]); });
                //Task<TResult> task = new Task<TResult>(func);
                //CacheItem newCacheItem = new CacheItem(key, task);
                // Or just:
                //CacheItem newCacheItem = new CacheItem(key, new Task<TResult>(() => this.memoizedMethod(arg)));
                // And finally more subclass-friendly:
                CacheItem newCacheItem = new CacheItem(key, new Task<TResult>(GetMethodClosure(args)));

                // The 'AddOrGetExisting' method is atomic: If a cached value for the key exists, the existing cached value is returned; otherwise null is returned as value property
                cacheItem = this.cache.AddOrGetExisting(newCacheItem, this.cacheItemPolicy);
                if (cacheItem.Value == null)
                {
                    //Console.WriteLine("OS thread ID=" + AppDomain.GetCurrentThreadId() + ", " + "Managed thread ID=" + Thread.CurrentThread.GetHashCode() + "/" + Thread.CurrentThread.ManagedThreadId + ": cacheItem.Value == null");
                    cacheItem = newCacheItem;
                    // The 'Start' method is idempotent
                    ((Task<TResult>)newCacheItem.Value).Start();
                    //((Task<TResult>)cacheItem.Value).Start();
                    Interlocked.Increment(ref this.numberOfTimesNoCacheInvoked);
                    ConditionalLogging("(Possibly expensive) async caching function execution #" + this.numberOfTimesNoCacheInvoked);
                }
                else
                {
                    //Console.WriteLine("OS thread ID=" + AppDomain.GetCurrentThreadId() + ", " + "Managed thread ID=" + Thread.CurrentThread.GetHashCode() + "/" + Thread.CurrentThread.ManagedThreadId + ": cacheItem.Value == " + cacheItem.Value);
                }
            }
            else
            {
                //Console.WriteLine("OS thread ID=" + AppDomain.GetCurrentThreadId() + ", " + "Managed thread ID=" + Thread.CurrentThread.GetHashCode() + "/" + Thread.CurrentThread.ManagedThreadId + ": cacheItem == " + cacheItem);
            }

            // The 'Result' property blocks until a value is available
            //Console.WriteLine("OS thread ID=" + AppDomain.GetCurrentThreadId() + ", " + "Managed thread ID=" + Thread.CurrentThread.GetHashCode() + "/" + Thread.CurrentThread.ManagedThreadId + ": Status: " + ((Task<TResult>)cacheItem.Value).Status);
            var retVal = ((Task<TResult>)cacheItem.Value).Result;
            //Console.WriteLine("OS thread ID=" + AppDomain.GetCurrentThreadId() + ", " + "Managed thread ID=" + Thread.CurrentThread.GetHashCode() + "/" + Thread.CurrentThread.ManagedThreadId + ": Invoke(" + args + ") took " + (DateTime.Now.Ticks - startTime) + " ticks");

            Interlocked.Increment(ref this.numberOfTimesInvoked);
            ConditionalLogging("Invocation #" + this.numberOfTimesInvoked + " took " + (DateTime.Now.Ticks - startTime) + " ticks | " + (DateTime.Now.Ticks - startTime) / TimeSpan.TicksPerMillisecond + " ms");

            return retVal;
        }
    }


    internal class Memoizer<TParam1, TResult> : AbstractMemoizer<TResult>, IMemoizer<TParam1, TResult>
    {
        readonly Func<TParam1, TResult> methodToBeMemoized;

        internal Memoizer(Func<TParam1, TResult> methodToBeMemoized, CacheItemPolicy cacheItemPolicy = null)
        {
            this.methodToBeMemoized = methodToBeMemoized;
            this.cache = new MemoryCache(MemoizerHelper.CreateMethodHash(this.methodToBeMemoized));
            this.cacheItemPolicy = cacheItemPolicy;
        }

        protected override Func<TResult> GetMethodClosure(params object[] args)
        {
            //return new Func<TResult>(delegate() { return this.methodToBeMemoized((TParam)args[0]); });
            // Or just:
            return () => this.methodToBeMemoized((TParam1)args[0]);
        }

        public TResult InvokeWith(TParam1 param)
        {
            return Invoke(param);
        }
    }


    internal class Memoizer<TParam1, TParam2, TResult> : AbstractMemoizer<TResult>, IMemoizer<TParam1, TParam2, TResult>
    {
        readonly Func<TParam1, TParam2, TResult> methodToBeMemoized;
        internal Memoizer(Func<TParam1, TParam2, TResult> methodToBeMemoized, CacheItemPolicy cacheItemPolicy = null)
        {
            this.methodToBeMemoized = methodToBeMemoized;
            this.cache = new MemoryCache(MemoizerHelper.CreateMethodHash(this.methodToBeMemoized));
            this.cacheItemPolicy = cacheItemPolicy;
        }
        protected override Func<TResult> GetMethodClosure(params object[] args) { return () => this.methodToBeMemoized((TParam1)args[0], (TParam2)args[1]); }
        public TResult InvokeWith(TParam1 param1, TParam2 param2) { return Invoke(param1, param2); }
    }


    internal class Memoizer<TParam1, TParam2, TParam3, TResult> : AbstractMemoizer<TResult>, IMemoizer<TParam1, TParam2, TParam3, TResult>
    {
        readonly Func<TParam1, TParam2, TParam3, TResult> methodToBeMemoized;
        internal Memoizer(Func<TParam1, TParam2, TParam3, TResult> methodToBeMemoized, CacheItemPolicy cacheItemPolicy = null)
        {
            this.methodToBeMemoized = methodToBeMemoized;
            this.cache = new MemoryCache(MemoizerHelper.CreateMethodHash(this.methodToBeMemoized));
            this.cacheItemPolicy = cacheItemPolicy;
        }
        protected override Func<TResult> GetMethodClosure(params object[] args) { return () => this.methodToBeMemoized((TParam1)args[0], (TParam2)args[1], (TParam3)args[2]); }
        public TResult InvokeWith(TParam1 param1, TParam2 param2, TParam3 param3) { return Invoke(param1, param2, param3); }
    }


    internal class Memoizer<TParam1, TParam2, TParam3, TParam4, TResult> : AbstractMemoizer<TResult>, IMemoizer<TParam1, TParam2, TParam3, TParam4, TResult>
    {
        readonly Func<TParam1, TParam2, TParam3, TParam4, TResult> methodToBeMemoized;
        internal Memoizer(Func<TParam1, TParam2, TParam3, TParam4, TResult> methodToBeMemoized, CacheItemPolicy cacheItemPolicy = null)
        {
            this.methodToBeMemoized = methodToBeMemoized;
            this.cache = new MemoryCache(MemoizerHelper.CreateMethodHash(this.methodToBeMemoized));
            this.cacheItemPolicy = cacheItemPolicy;
        }
        protected override Func<TResult> GetMethodClosure(params object[] args) { return () => this.methodToBeMemoized((TParam1)args[0], (TParam2)args[1], (TParam3)args[2], (TParam4)args[3]); }
        public TResult InvokeWith(TParam1 param1, TParam2 param2, TParam3 param3, TParam4 param4) { return Invoke(param1, param2, param3, param4); }
    }
    #endregion

    #region MemoryCacheMemoizer
    [Obsolete("Just a demo - use Memoizer class - its much cooler")]
    public class MemoryCacheMemoizer<TResult> : IDisposable
    {
        const CacheItemPolicy NO_CACHE_POLICY = null;

        string name;
        MemoryCache cache;

        public MemoryCacheMemoizer(Type sourceClass, string nameOfMethodToBeMemoized)
        {
            this.name = MemoizerHelper.CreateMethodHash(sourceClass, nameOfMethodToBeMemoized);
            this.cache = new MemoryCache(this.name);
        }

        public void Dispose() { this.cache.Dispose(); }

        public void Clear()
        {
            Dispose();
            this.cache = new MemoryCache(name);
        }

        public TResult Invoke<TParam1, TParam2>(Func<TParam1, TParam2, TResult> memoizedMethod, TParam1 arg1, TParam2 arg2)
        {
            string key = MemoizerHelper.CreateParameterHash(arg1, arg2);
            TResult retVal = (TResult)this.cache.Get(key);
            if (retVal != null)
                return retVal;

            retVal = memoizedMethod.Invoke(arg1, arg2);
            this.cache.Add(key, retVal, NO_CACHE_POLICY);
            return retVal;
        }
    }
    #endregion

    #region ConcurrentDictionaryMemoizer
    [Obsolete("Just a demo - use Memoizer class - its much cooler")]
    public class ConcurrentDictionaryMemoizer<TResult>
    {
        readonly ConcurrentDictionary<object, TResult> cache = new ConcurrentDictionary<object, TResult>();

        public void Clear() { this.cache.Clear(); }

        public TResult Invoke<TParam1, TParam2>(Func<TParam1, TParam2, TResult> memoizedMethod, TParam1 arg1, TParam2 arg2)
        {
            string key = MemoizerHelper.CreateParameterHash(arg1, arg2);
            TResult retVal;
            if (this.cache.TryGetValue(key, out retVal))
                return retVal;

            retVal = memoizedMethod.Invoke(arg1, arg2);
            this.cache.TryAdd(key, retVal);
            return retVal;
        }
    }
    #endregion

    #region DictionaryMemoizer
    [Obsolete("Just a demo - use Memoizer class - its much cooler")]
    public class DictionaryMemoizer<TResult>
    {
        static readonly object @lock = new object();

        // Not thread-safe - all kinds of write access must be serialized
        readonly IDictionary<string, object> cache = new Dictionary<string, object>();

        public void Clear() { this.cache.Clear(); }

        public TResult Invoke<TParam1, TParam2>(Func<TParam1, TParam2, TResult> memoizedMethod, TParam1 arg1, TParam2 arg2)
        {
            string key = MemoizerHelper.CreateParameterHash(arg1, arg2);
            if (this.cache.ContainsKey(key))
                return (TResult)cache[key];

            lock (@lock)
            {
                if (this.cache.ContainsKey(key))
                    return (TResult)cache[key];
                TResult retVal = memoizedMethod.Invoke(arg1, arg2);
                this.cache.Add(key, retVal);
                return retVal;
            }
        }
    }
    #endregion
}
