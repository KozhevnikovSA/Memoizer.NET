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

    #region IInvocable
    /// <summary>
    /// Interface for invocable classes.
    /// </summary>
    public interface IInvocable<out TResult, in TParam>
    {
        TResult InvokeWith(TParam param);
    }
    public interface IInvocable<out TResult, in TParam1, in TParam2>
    {
        TResult InvokeWith(TParam1 param1, TParam2 param2);
    }
    public interface IInvocable<out TResult, in TParam1, in TParam2, in TParam3>
    {
        TResult InvokeWith(TParam1 param1, TParam2 param2, TParam3 param3);
    }
    #endregion

    #region MemoizerHelper
    // TODO: write tests and improve algorithms!
    public class MemoizerHelper
    {
        public static string CreateParameterHash(params object[] args) { return string.Join(string.Empty, args); }
        public static string CreateMethodHash(Type sourceClass, string methodName) { return CreateParameterHash(sourceClass.ToString(), methodName); }
    }
    #endregion

    #region Memoizer (using a MemoryCache instance and Goetz's algorithm)
    /// <remarks>
    /// This class is an implementation of a method-level/fine-grained cache (a.k.a. <i>memoizer</i>).
    /// It is based on an implementation from the book "Java Concurrency in Practice" by Brian Goetz et. al. -
    /// ported to C# 4.0 using goodness like method handlers/delegates and lambda expressions.
    /// <p/>
    /// A <code>System.Runtime.Caching.MemoryCache</code> instance is used as cache, enabling configuration through the <code>System.Runtime.Caching.CacheItemPolicy</code>.
    /// Default cache configuration is: items to be held as long as the CLR is alive or the memoizer is disposed/cleared.
    /// Every <code>Memoizer</code> instance creates its own <code>System.Runtime.Caching.MemoryCache</code> instance.
    /// This is due to the fact that <code>Memoizer</code> intances are <i>disposable</i>, hence will dispose of its <code>Memoizer</code> member.
    /// One could re-design this to utilize the ubiquitous <i>default</i> <code>System.Runtime.Caching.MemoryCache</code> instance to make this memoizer even faster.
    /// <p/>
    /// This class is thread-safe.
    /// </remarks>
    /// <author>Eirik Torske</author>
    public abstract class AbstractMemoizer<TResult> : IThreadSafe, IDisposable
    {
        protected string name;
        protected MemoryCache cache;
        protected CacheItemPolicy cacheItemPolicy;

        //protected readonly Func<TParam, TResult> methodToBeMemoized;

        //protected AbstractMemoizer(string methodHash,
        //                           Func<TParam, TResult> methodToBeMemoized,
        //                           CacheItemPolicy cacheItemPolicy = null)
        //{
        //    if (string.IsNullOrEmpty(methodHash)) { throw new ArgumentException("A hash of the method to be memoized must be provided"); }
        //    //if (methodToBeMemoized == null) { throw new ArgumentException("Method to be memoized is missing"); } // 'methodToBeMemoized' is overloaded field
        //    this.name = MemoizerHelper.CreateParameterHash(methodHash);
        //    this.cache = new MemoryCache(name);
        //    this.methodToBeMemoized = methodToBeMemoized;
        //    this.cacheItemPolicy = cacheItemPolicy;
        //}

        //protected AbstractMemoizer(Type sourceClass,
        //                           string nameOfMethodToBeMemoized,
        //                           Func<TParam, TResult> methodToBeMemoized,
        //                           CacheItemPolicy cacheItemPolicy = null)
        //    : this(MemoizerHelper.CreateMethodHash(sourceClass, nameOfMethodToBeMemoized), methodToBeMemoized, cacheItemPolicy)
        //{
        //    if (sourceClass == null) { throw new ArgumentException("Type of invoking class is missing"); }
        //    if (string.IsNullOrEmpty(nameOfMethodToBeMemoized)) { throw new ArgumentException("Name of method to be memoized is missing"); }
        //}

        /// <summary>
        /// Disposes of the memoizer.
        /// </summary>
        public void Dispose() { this.cache.Dispose(); }

        /// <summary>
        /// Clears the cache, removing all items.
        /// </summary>
        public void Clear()
        {
            //int i = 0;
            foreach (var element in this.cache.AsEnumerable())
            {
                //Console.WriteLine("Removing cached element #" + ++i + ": " + element.Key + "=" + ((Task<string>)element.Value).Status);
                this.cache.Remove(element.Key);
            }
            //Console.WriteLine("All " + i + " elements in memoizer removed");
        }


        /// <summary>
        /// Gets the method delegate, closed under given arguments.
        /// </summary>
        protected abstract Func<TResult> GetMethodClosure(params object[] args);
        //protected virtual Func<TResult> GetMethodClosure(params object[] args)
        //{
        //    //return new Func<TResult>(delegate() { return this.methodToBeMemoized((TParam)args[0]); });
        //    // Or just:
        //    return () => this.methodToBeMemoized((TParam)args[0]);
        //}

        //public abstract TResult InvokeWith(TParam param)
        //{
        //    return Invoke(param);
        //}

        /// <summary>
        /// Invokes the method delegate - consulting the cache on the way.
        /// </summary>
        protected TResult Invoke(params object[] args)
        {
            //long startTime = DateTime.Now.Ticks;
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

                // The 'AddOrGetExisting' method is atomic: If a cached value for the key exists, the existing cached value is returned; otherwise null is returned
                cacheItem = this.cache.AddOrGetExisting(newCacheItem, this.cacheItemPolicy);
                if (cacheItem.Value == null)
                {
                    //Console.WriteLine("OS thread ID=" + AppDomain.GetCurrentThreadId() + ", " + "Managed thread ID=" + Thread.CurrentThread.GetHashCode() + "/" + Thread.CurrentThread.ManagedThreadId + ": cacheItem.Value == null");
                    cacheItem = newCacheItem;
                    // The 'Start' method is idempotent
                    ((Task<TResult>)newCacheItem.Value).Start();
                }
                //    else
                //    {
                //        Console.WriteLine("OS thread ID=" + AppDomain.GetCurrentThreadId() + ", " + "Managed thread ID=" + Thread.CurrentThread.GetHashCode() + "/" + Thread.CurrentThread.ManagedThreadId + ": cacheItem.Value == " + cacheItem.Value);
                //    }
                //}
                //else
                //{
                //    Console.WriteLine("OS thread ID=" + AppDomain.GetCurrentThreadId() + ", " + "Managed thread ID=" + Thread.CurrentThread.GetHashCode() + "/" + Thread.CurrentThread.ManagedThreadId + ": cacheItem == " + cacheItem);
            }

            // The 'Result' property blocks until a value is available
            //Console.WriteLine("OS thread ID=" + AppDomain.GetCurrentThreadId() + ", " + "Managed thread ID=" + Thread.CurrentThread.GetHashCode() + "/" + Thread.CurrentThread.ManagedThreadId + ": Status: " + ((Task<TResult>)cacheItem.Value).Status);
            var retVal = ((Task<TResult>)cacheItem.Value).Result;
            //Console.WriteLine("OS thread ID=" + AppDomain.GetCurrentThreadId() + ", " + "Managed thread ID=" + Thread.CurrentThread.GetHashCode() + "/" + Thread.CurrentThread.ManagedThreadId + ": Invoke(" + args + ") took " + (DateTime.Now.Ticks - startTime) + " ticks");

            return retVal;
        }
    }


    public class Memoizer<TResult, TParam> : AbstractMemoizer<TResult>, IInvocable<TResult, TParam>
    {
        readonly Func<TParam, TResult> methodToBeMemoized;

        public Memoizer(string methodHash,
                        Func<TParam, TResult> methodToBeMemoized,
                        CacheItemPolicy cacheItemPolicy = null)
        {
            if (string.IsNullOrEmpty(methodHash)) { throw new ArgumentException("A hash of the method to be memoized must be provided"); }
            //if (methodToBeMemoized == null) { throw new ArgumentException("Method to be memoized is missing"); } // 'methodToBeMemoized' is overloaded field
            this.name = MemoizerHelper.CreateParameterHash(methodHash);
            this.cache = new MemoryCache(name);
            this.methodToBeMemoized = methodToBeMemoized;
            this.cacheItemPolicy = cacheItemPolicy;
        }

        public Memoizer(Type sourceClass,
                        string nameOfMethodToBeMemoized,
                        Func<TParam, TResult> methodToBeMemoized,
                        CacheItemPolicy cacheItemPolicy = null)
            : this(MemoizerHelper.CreateMethodHash(sourceClass, nameOfMethodToBeMemoized), methodToBeMemoized, cacheItemPolicy)
        {
            if (sourceClass == null) { throw new ArgumentException("Type of invoking class is missing"); }
            if (string.IsNullOrEmpty(nameOfMethodToBeMemoized)) { throw new ArgumentException("Name of method to be memoized is missing"); }
        }

        protected override Func<TResult> GetMethodClosure(params object[] args)
        {
            //return new Func<TResult>(delegate() { return this.methodToBeMemoized((TParam)args[0]); });
            // Or just:
            return () => this.methodToBeMemoized((TParam)args[0]);
        }

        public TResult InvokeWith(TParam param)
        {
            return Invoke(param);
        }
    }


    public class Memoizer<TResult, TParam1, TParam2> : AbstractMemoizer<TResult>, IInvocable<TResult, TParam1, TParam2>
    {
        readonly Func<TParam1, TParam2, TResult> methodToBeMemoized;

        public Memoizer(string methodHash,
                        Func<TParam1, TParam2, TResult> methodToBeMemoized,
                        CacheItemPolicy cacheItemPolicy = null)
        {
            if (string.IsNullOrEmpty(methodHash)) { throw new ArgumentException("A hash of the method to be memoized must be provided"); }
            //if (methodToBeMemoized == null) { throw new ArgumentException("Method to be memoized is missing"); } // 'methodToBeMemoized' is overloaded field
            this.name = MemoizerHelper.CreateParameterHash(methodHash);
            this.cache = new MemoryCache(name);
            this.methodToBeMemoized = methodToBeMemoized;
            this.cacheItemPolicy = cacheItemPolicy;
        }

        public Memoizer(Type sourceClass,
                        string nameOfMethodToBeMemoized,
                        Func<TParam1, TParam2, TResult> methodToBeMemoized,
                        CacheItemPolicy cacheItemPolicy = null)
            : this(MemoizerHelper.CreateMethodHash(sourceClass, nameOfMethodToBeMemoized), methodToBeMemoized, cacheItemPolicy)
        {
            if (sourceClass == null) { throw new ArgumentException("Type of invoking class is missing"); }
            if (string.IsNullOrEmpty(nameOfMethodToBeMemoized)) { throw new ArgumentException("Name of method to be memoized is missing"); }
        }

        protected override Func<TResult> GetMethodClosure(params object[] args) { return () => this.methodToBeMemoized((TParam1)args[0], (TParam2)args[1]); }

        public TResult InvokeWith(TParam1 param1, TParam2 param2) { return Invoke(param1, param2); }
    }


    public class Memoizer<TResult, TParam1, TParam2, TParam3> : AbstractMemoizer<TResult>, IInvocable<TResult, TParam1, TParam2, TParam3>
    {
        readonly Func<TParam1, TParam2, TParam3, TResult> methodToBeMemoized;

        public Memoizer(string methodHash,
                        Func<TParam1, TParam2, TParam3, TResult> methodToBeMemoized,
                        CacheItemPolicy cacheItemPolicy = null)
        {
            if (string.IsNullOrEmpty(methodHash)) { throw new ArgumentException("A hash of the method to be memoized must be provided"); }
            //if (methodToBeMemoized == null) { throw new ArgumentException("Method to be memoized is missing"); } // 'methodToBeMemoized' is overloaded field
            this.name = MemoizerHelper.CreateParameterHash(methodHash);
            this.cache = new MemoryCache(name);
            this.methodToBeMemoized = methodToBeMemoized;
            this.cacheItemPolicy = cacheItemPolicy;
        }

        public Memoizer(Type sourceClass,
                        string nameOfMethodToBeMemoized,
                        Func<TParam1, TParam2, TParam3, TResult> methodToBeMemoized,
                        CacheItemPolicy cacheItemPolicy = null)
            : this(MemoizerHelper.CreateMethodHash(sourceClass, nameOfMethodToBeMemoized), methodToBeMemoized, cacheItemPolicy)
        {
            if (sourceClass == null) { throw new ArgumentException("Type of invoking class is missing"); }
            if (string.IsNullOrEmpty(nameOfMethodToBeMemoized)) { throw new ArgumentException("Name of method to be memoized is missing"); }
        }

        protected override Func<TResult> GetMethodClosure(params object[] args) { return () => this.methodToBeMemoized((TParam1)args[0], (TParam2)args[1], (TParam3)args[2]); }

        public TResult InvokeWith(TParam1 param1, TParam2 param2, TParam3 param3) { return Invoke(param1, param2, param3); }


        //public Memoizer(Type sourceClass,
        //                string nameOfMethodToBeMemoized,
        //                Func<TParam1, TParam2, TParam3, TResult> methodToBeMemoized,
        //                CacheItemPolicy cacheItemPolicy = null)
        //    : base(sourceClass, nameOfMethodToBeMemoized, null, cacheItemPolicy)
        //{
        //    if (methodToBeMemoized == null) { throw new ArgumentException("Method to be memoized is missing"); }
        //    this.methodToBeMemoized = methodToBeMemoized;
        //}

        //protected override Func<TResult> GetMethodClosure(params object[] args)
        //{
        //    return () => this.methodToBeMemoized((TParam1)args[0], (TParam2)args[1], (TParam3)args[2]);
        //}
    }


    // TODO: ...
    //public class Memoizer<TResult, TParam1, TParam2, TParam3, TParam4> : Memoizer<TResult, TParam1>
    //{
    //    readonly Func<TParam1, TParam2, TParam3, TParam4, TResult> methodToBeMemoized;

    //    public Memoizer(Type sourceClass,
    //                    string nameOfMethodToBeMemoized,
    //                    Func<TParam1, TParam2, TParam3, TParam4, TResult> methodToBeMemoized,
    //                    CacheItemPolicy cacheItemPolicy = null)
    //        : base(sourceClass, nameOfMethodToBeMemoized, null, cacheItemPolicy)
    //    {
    //        if (methodToBeMemoized == null) { throw new ArgumentException("Method to be memoized is missing"); }
    //        this.methodToBeMemoized = methodToBeMemoized;
    //    }

    //    protected override Func<TResult> GetMethodClosure(params object[] args)
    //    {
    //        return () => this.methodToBeMemoized((TParam1)args[0], (TParam2)args[1], (TParam3)args[2], (TParam4)args[3]);
    //    }
    //}


    // TODO: ...
    //public class Memoizer<TResult, TParam1, TParam2, TParam3, TParam4, TParam5> : Memoizer<TResult, TParam1>
    //{
    //    readonly Func<TParam1, TParam2, TParam3, TParam4, TParam5, TResult> methodToBeMemoized;

    //    public Memoizer(Type sourceClass,
    //                    string nameOfMethodToBeMemoized,
    //                    Func<TParam1, TParam2, TParam3, TParam4, TParam5, TResult> methodToBeMemoized,
    //                    CacheItemPolicy cacheItemPolicy = null)
    //        : base(sourceClass, nameOfMethodToBeMemoized, null, cacheItemPolicy)
    //    {
    //        if (methodToBeMemoized == null) { throw new ArgumentException("Method to be memoized is missing"); }
    //        this.methodToBeMemoized = methodToBeMemoized;
    //    }

    //    protected override Func<TResult> GetMethodClosure(params object[] args)
    //    {
    //        return () => this.methodToBeMemoized((TParam1)args[0], (TParam2)args[1], (TParam3)args[2], (TParam4)args[3], (TParam5)args[4]);
    //    }
    //}


    // TODO: ...
    //public class Memoizer<TResult, TParam1, TParam2, TParam3, TParam4, TParam5, TParam6> : Memoizer<TResult, TParam1>
    //{
    //    readonly Func<TParam1, TParam2, TParam3, TParam4, TParam5, TParam6, TResult> methodToBeMemoized;

    //    public Memoizer(Type sourceClass,
    //                    string nameOfMethodToBeMemoized,
    //                    Func<TParam1, TParam2, TParam3, TParam4, TParam5, TParam6, TResult> methodToBeMemoized,
    //                    CacheItemPolicy cacheItemPolicy = null)
    //        : base(sourceClass, nameOfMethodToBeMemoized, null, cacheItemPolicy)
    //    {
    //        if (methodToBeMemoized == null) { throw new ArgumentException("Method to be memoized is missing"); }
    //        this.methodToBeMemoized = methodToBeMemoized;
    //    }

    //    protected override Func<TResult> GetMethodClosure(params object[] args)
    //    {
    //        return () => this.methodToBeMemoized((TParam1)args[0], (TParam2)args[1], (TParam3)args[2], (TParam4)args[3], (TParam5)args[4], (TParam6)args[5]);
    //    }
    //}
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
