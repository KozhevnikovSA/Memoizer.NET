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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace Memoizer.NET.Test
{

    [TestFixture]
    class TwoPhaseExecutorTests
    {

        ///// <summary>
        ///// Well, not really a test... rather an instrumented demo
        ///// </summary>
        ////[Ignore("Temporary disabled...")]
        //[Test]
        //public void Test()
        //{
        //    int NUMBER_OF_PARTICIPANTS = 6;

        //    // 1. Create the two-phase executor
        //    TwoPhaseExecutor twoPhaseExecutor = new TwoPhaseExecutor(NUMBER_OF_PARTICIPANTS);
        //    twoPhaseExecutor.Instrumentation = true;

        //    // 2. Create and start all worker threads
        //    if (NUMBER_OF_PARTICIPANTS % 2 == 0)
        //        for (int i = 0; i < NUMBER_OF_PARTICIPANTS / 2; ++i)
        //        {
        //            new TrivialTask(twoPhaseExecutor.Barrier).Start();
        //            new SomeOtherTask(twoPhaseExecutor.Barrier).Start();
        //        }
        //    else
        //        for (int i = 0; i < NUMBER_OF_PARTICIPANTS; ++i)
        //            new SomeOtherTask(twoPhaseExecutor.Barrier).Start();

        //    // 3. Start the two-phase executor
        //    twoPhaseExecutor.Start();
        //}

        //class SomeOtherTask : AbstractTwoPhaseExecutorThread
        //{
        //    static int TASK_COUNTER;

        //    public SomeOtherTask(Barrier barrier)
        //        : base(barrier, true)
        //    {
        //        TaskNumber = Interlocked.Increment(ref TASK_COUNTER);
        //        ParticipantNumber = Interlocked.Increment(ref PARTICIPANT_COUNTER);
        //        Action = () => Console.WriteLine("Barrier participant #" + ParticipantNumber + " [invocation #" + ExecutionIndex + "] [" + ThreadInfo + "]");

        //        if (Instrumented)
        //            Console.WriteLine(this.GetType().Name + " #" + TaskNumber + " created... [(possible) barrier participant #" + ParticipantNumber + "]");
        //    }
        //}





        static IList<string> LOG_LINES;

        readonly Action<string> loggingMethod = delegate(string logLine) { LOG_LINES.Add(logLine); Console.WriteLine(logLine); };

        [SetUp]
        public void PerMethodSetUp()
        {
            LOG_LINES = new List<string>();
        }


        // --- less-than-one execution numbers ---

        [Test, ExpectedException(typeof(ArgumentException), ExpectedMessage = "Number-of-iterations parameter ('iterations') cannot be less than 1", MatchType = MessageMatch.Exact)]
        public void TwoPhaseExecutionContext_IterationParameterMustBeAPositiveInteger(
            [Values(-100, -1, 0)] int numberOfIterations,
            [Values(-100, -1, 0, 1)] int numberOfConcurrentWorkerThreads)
        {
            Func<string, long, string> myFunc = MemoizerTests.ReallySlowNetworkStaticInvocation;
            myFunc.CreateExecutionContext(iterations: numberOfIterations, threads: numberOfConcurrentWorkerThreads);
        }


        [Test, ExpectedException(typeof(ArgumentException), ExpectedMessage = "Number-of-worker-threads parameter ('threads') cannot be less than 1", MatchType = MessageMatch.Exact)]
        public void TwoPhaseExecutionContext_NumberOfConcurrentWorkerThreadsParameterMustBeAPositiveInteger(
            [Values(1, 100)] int numberOfIterations,
            [Values(-100, -1, 0)] int numberOfConcurrentWorkerThreads)
        {
            Func<string, long, string> myFunc = MemoizerTests.ReallySlowNetworkStaticInvocation;
            myFunc.CreateExecutionContext(iterations: numberOfIterations, threads: numberOfConcurrentWorkerThreads);
        }


        // --- no-execution behaviour ---

        [Test, ExpectedException(typeof(ApplicationException), ExpectedMessage = "No results are available", MatchType = MessageMatch.Exact)]
        public void NotExecutedPhasedExecutionContext_ReportIsFalseByDefault(
            [Values(1, 2, 100)] int iterations,
            [Values(1, 2, 100)] int threads)
        {
            int invocationCounter = 0;
            Func<int> myThreadSafeFunc = () => ++invocationCounter;

            TwoPhaseExecutionContext twoPhaseExecutionContext = myThreadSafeFunc.CreatePhasedExecutionContext(iterations: iterations, threads: threads);
            Assert.That(LOG_LINES, Is.Empty);
            var res = twoPhaseExecutionContext.Results[0];
        }


        [Test, ExpectedException(typeof(ApplicationException), ExpectedMessage = "No results are available", MatchType = MessageMatch.Exact)]
        public void NotExecutedPhasedExecutionContext_LoggingMethodIsConsoleWriteLineByDefault(
            [Values(1, 2, 100)] int iterations,
            [Values(1, 2, 100)] int threads)
        {
            int invocationCounter = 0;
            Func<int> myThreadSafeFunc = () => ++invocationCounter;

            TwoPhaseExecutionContext twoPhaseExecutionContext = myThreadSafeFunc.CreatePhasedExecutionContext(iterations: iterations, threads: threads, report: true);

            // Console.WriteLine by default only
            Assert.That(LOG_LINES, Is.Empty);

            var res = twoPhaseExecutionContext.Results[0][1000];
        }


        [Test, ExpectedException(typeof(ApplicationException), ExpectedMessage = "No results are available", MatchType = MessageMatch.Exact)]
        public void NotExecutedPhasedExecutionContext_ShouldGiveNotYetExecutedMessage_1()
        {
            int invocationCounter = 0;
            Func<int> myThreadSafeFunc = () => ++invocationCounter;

            TwoPhaseExecutionContext twoPhaseExecutionContext = myThreadSafeFunc.CreatePhasedExecutionContext(iterations: 1, threads: 1, report: true, loggingMethod: loggingMethod);

            Assert.That(LOG_LINES.Count, Is.EqualTo(1));
            Assert.That(LOG_LINES[0], Is.EqualTo("PhasedExecutor: 1 round of {[1 concurrent thread, no args, latency unknown]} - not yet executed"));

            var res = twoPhaseExecutionContext.Results[0];
        }


        [Test, ExpectedException(typeof(ApplicationException), ExpectedMessage = "Result set contains only 2 iterations... Really no point is asking for iteration #3 (zero-based) then, is it?", MatchType = MessageMatch.Exact)]
        public void NotExecutedPhasedExecutionContext_ShouldGiveNotYetExecutedMessage_2()
        {
            int invocationCounter = 0;
            Func<int> myThreadSafeFunc = () => ++invocationCounter;

            TwoPhaseExecutionContext twoPhaseExecutionContext = myThreadSafeFunc.CreatePhasedExecutionContext(iterations: 2, threads: 3, report: true, loggingMethod: loggingMethod);

            Assert.That(LOG_LINES.Count, Is.EqualTo(1));
            Assert.That(LOG_LINES[0], Is.EqualTo("PhasedExecutor: 2 rounds of {[3 concurrent threads, no args, latency unknown]} - not yet executed"));

            var res = twoPhaseExecutionContext.Results[2];
        }


        //[Test, ExpectedException(typeof(ApplicationException), ExpectedMessage = "No results are available", MatchType = MessageMatch.Exact)]
        //public void ZeroThreadPhasedExecutionContext_ShouldGiveNotApplicableMessage_1_2()
        //{
        //    long invocationCounter = 0;
        //    Func<long> myThreadSafeFunc = () => invocationCounter++;

        //    TwoPhaseExecutionContext twoPhaseExecutionContext = myThreadSafeFunc.CreatePhasedExecutionContext(iterations: 1, threads: 0, report: true, loggingMethod: loggingMethod);

        //    Assert.That(LOG_LINES.Count, Is.EqualTo(1));
        //    Assert.That(LOG_LINES[0], Is.EqualTo("PhasedExecutor: 1 round of {[0 concurrent threads, no args, latency unknown]} (no worker threads are declared - context cannot be executed!)"));

        //    IEnumerable<object> results = twoPhaseExecutionContext.Results[0];
        //}


        //[Test, ExpectedException(typeof(ArgumentException), ExpectedMessage = "No iterations are declared - context cannot be executed!", MatchType = MessageMatch.Exact)]
        //public void ZeroThreadPhasedExecutionContext_ShouldGiveNotApplicableMessage_2_1(
        //    [Values(-1, 0, 1, 10)] int threads)
        //{
        //    long invocationCounter = 0;
        //    Func<long> myThreadSafeFunc = () => invocationCounter++;
        //    myThreadSafeFunc.CreatePhasedExecutionContext(iterations: 0, threads: threads).Execute();
        //}


        //[Test, ExpectedException(typeof(InvalidOperationException), ExpectedMessage = "No worker threads are declared - context cannot be executed!", MatchType = MessageMatch.Exact)]
        //public void ZeroThreadPhasedExecutionContext_ShouldGiveNotApplicableMessage_2_2(
        //    [Values(-1, 0, 1, 10)] int iterations)
        //{
        //    long invocationCounter = 0;
        //    Func<long> myThreadSafeFunc = () => invocationCounter++;
        //    myThreadSafeFunc.CreatePhasedExecutionContext(iterations: iterations, threads: 0).Execute();
        //}


        // --- default argument's execution => single iteration - single thread ---

        [Test, ExpectedException(typeof(ApplicationException), ExpectedMessage = "No results are available", MatchType = MessageMatch.Exact)]
        public void DefaultPhasedExecutionContext_NotExecuted_ReportingIsOffByDefault_1()
        {
            int invocationCounter = 0;
            Func<int> myThreadSafeFunc = () => ++invocationCounter;

            TwoPhaseExecutionContext twoPhaseExecutionContext = myThreadSafeFunc.CreatePhasedExecutionContext(loggingMethod: loggingMethod);
            Assert.That(LOG_LINES, Is.Empty);
            var res = twoPhaseExecutionContext.Results[0];
        }


        [Test, ExpectedException(typeof(ApplicationException), ExpectedMessage = "Result set contains only 1 iteration... Really no point is asking for iteration #2 (zero-based) then, is it?", MatchType = MessageMatch.Exact)]
        public void DefaultPhasedExecutionContext_NotExecuted_ReportingIsOffByDefault_2()
        {
            int invocationCounter = 0;
            Func<int> myThreadSafeFunc = () => ++invocationCounter;

            TwoPhaseExecutionContext twoPhaseExecutionContext = myThreadSafeFunc.CreatePhasedExecutionContext(loggingMethod: loggingMethod);
            Assert.That(LOG_LINES, Is.Empty);
            var res = twoPhaseExecutionContext.Results[1];
        }


        [Test]
        public void DefaultPhasedExecutionContext_Executed_AllIndividualResultsAreAvailable_ReportingIsOnByDefault()
        {
            int invocationCounter = 0;
            Func<int> myThreadSafeFunc = () => ++invocationCounter;

            TwoPhaseExecutionContext twoPhaseExecutionContext = myThreadSafeFunc.CreatePhasedExecutionContext(loggingMethod: loggingMethod, tag: "f1")
                .Execute();

            Assert.That(LOG_LINES.Count, Is.EqualTo(1));
            Assert.That(LOG_LINES[0].StartsWith("PhasedExecutor: 1 round of {[f1: 1 concurrent thread, no args, latency unknown]} took "));
            Assert.That(LOG_LINES[0].Contains(" | "));
            Assert.That(LOG_LINES[0].EndsWith(" ticks"));

            Assert.That(twoPhaseExecutionContext.Results[0][0], Is.EqualTo(1)); // 1 == first invocationCounter count

            // Or in a more generic form:
            PhasedExecutionContextResult executionResults = twoPhaseExecutionContext.Results;
            Assert.That(executionResults, Is.Not.Null);
            Assert.That(executionResults.Count, Is.EqualTo(1));
            for (int i = 0; i < executionResults.Count; ++i)
            {
                Assert.That(executionResults[i][0], Is.EqualTo(1)); // 1 == first invocationCounter count

                IList<object> iterationResults = twoPhaseExecutionContext.Results[i];
                Assert.That(iterationResults.Count, Is.EqualTo(1));
                for (int j = 0; j < iterationResults.Count; ++j)
                {
                    Assert.That(iterationResults, Is.Not.Null);
                    Assert.That(iterationResults.Count, Is.EqualTo(1));
                    Assert.That(iterationResults[j], Is.EqualTo(i + 1));
                }
            }
        }


        [Test]
        public void DefaultPhasedExecutionContext_ExecutedAndVerified_ReportingIsOnByDefault()
        {
            int invocationCounter = 0;
            Func<int> myThreadSafeFunc = () => ++invocationCounter;

            myThreadSafeFunc
                .CreatePhasedExecutionContext(args: null, threads: 1, iterations: 1, report: false, concurrent: true, memoize: false, functionLatency: default(long), instrumentation: false, tag: null, loggingMethod: loggingMethod)
                .Execute(measureLatency: false, assertLatency: false, report: true)
                .Verify(report: true, listResults: false, expectedResults: default(IDictionary<string, object>), expectedMinimumLatency: 0L, expectedMaximumLatency: Int64.MaxValue, actualFunctionInvocationCounts: default(IDictionary<string, long>));

            Assert.That(LOG_LINES.Count, Is.EqualTo(2));
            Assert.That(LOG_LINES[1], Is.EqualTo("PhasedExecutor: 1 round of {[1 concurrent thread, no args, latency unknown]} expected results not given, latency boundaries demands not given"));
        }


        [Test]
        public void DefaultPhasedExecutionContext_ExecutedAndVerified_ReportingOnNonExecutedContextActivated()
        {
            int invocationCounter = 0;
            Func<int> myThreadSafeFunc = () => ++invocationCounter;

            myThreadSafeFunc
                .CreatePhasedExecutionContext(report: true, loggingMethod: loggingMethod)
                //.CreatePhasedExecutionContext(args: null, threads: 1, iterations: 1, report: true, concurrent: true, memoize: false, functionLatency: default(long), instrumentation: false, tag: null, loggingMethod: loggingMethod)
                .Execute() //measureLatency: false, assertLatency: false, report: true)
                .Verify();//report: true, listResults: false, expectedResults: default(IDictionary<string, object>), expectedMinimumLatency: 0L, expectedMaximumLatency: Int64.MaxValue, actualFunctionInvocationCounts: default(IDictionary<string, long>));

            Assert.That(LOG_LINES.Count, Is.EqualTo(3));
            Assert.That(LOG_LINES[0], Is.EqualTo("PhasedExecutor: 1 round of {[1 concurrent thread, no args, latency unknown]} - not yet executed"));
        }


        // HIT!

        [Test]
        public void DefaultPhasedExecutionContext_ExecutedAndVerified_EmbeddedLatencyMeasurement()
        {
            int invocationCounter = 0;
            Func<int> myThreadSafeFunc = () => ++invocationCounter;

            myThreadSafeFunc
                .CreatePhasedExecutionContext(loggingMethod: loggingMethod)
                //.CreatePhasedExecutionContext(args: null, threads: 1, iterations: 1, report: true, concurrent: true, memoize: false, functionLatency: default(long), instrumentation: false, tag: null, loggingMethod: loggingMethod)
                .Execute(measureLatency: true) //, assertLatency: false, report: true)
                .Verify();//report: true, listResults: false, expectedResults: default(IDictionary<string, object>), expectedMinimumLatency: 0L, expectedMaximumLatency: Int64.MaxValue, actualFunctionInvocationCounts: default(IDictionary<string, long>));

            Assert.That(LOG_LINES.Count, Is.EqualTo(2));
            Assert.That(LOG_LINES[0].StartsWith("PhasedExecutor: 1 round of {[1 concurrent thread, no args, latency unknown]} took "));
            Assert.That(LOG_LINES[0].Contains(" | "));
            Assert.That(LOG_LINES[0].EndsWith(" ticks"));
            Assert.That(LOG_LINES[1], Is.EqualTo("PhasedExecutor: 1 round of {[1 concurrent thread, no args, latency unknown]} expected results not given, latency boundaries demands not given"));
        }


        [Test]
        public void DefaultPhasedExecutionContext_ExecutedAndVerified_EmbeddedLatencyAsserts()
        {
            int invocationCounter = 0;
            Func<int> myThreadSafeFunc = () => ++invocationCounter;

            myThreadSafeFunc
                .CreatePhasedExecutionContext(loggingMethod: loggingMethod)
                //.CreatePhasedExecutionContext(args: null, threads: 1, iterations: 1, report: true, concurrent: true, memoize: false, functionLatency: default(long), instrumentation: false, tag: null, loggingMethod: loggingMethod)
                .Execute(measureLatency: true, assertLatency: true) //, report: true)
                .Verify();//report: true, listResults: false, expectedResults: default(IDictionary<string, object>), expectedMinimumLatency: 0L, expectedMaximumLatency: Int64.MaxValue, actualFunctionInvocationCounts: default(IDictionary<string, long>));

            Assert.That(LOG_LINES.Count, Is.EqualTo(2));
            Assert.That(LOG_LINES[0].StartsWith("PhasedExecutor: 1 round of {[1 concurrent thread, no args, latency unknown]} took "));
            Assert.That(LOG_LINES[0].Contains(" | "));
            Assert.That(LOG_LINES[0].EndsWith(" ticks"));
            Assert.That(LOG_LINES[1], Is.EqualTo("PhasedExecutor: 1 round of {[1 concurrent thread, no args, latency unknown]} expected results not given, internal latency boundaries demands: ..."));
        }


        [Test]
        public void DefaultPhasedExecutionContext_ExecutedAndVerified_EmbeddedLatencyAsserts_EmbeddedLatencyMeasuringIncluded()
        {
            int invocationCounter = 0;
            Func<int> myThreadSafeFunc = () => ++invocationCounter;

            myThreadSafeFunc
                .CreatePhasedExecutionContext(loggingMethod: loggingMethod)
                //.CreatePhasedExecutionContext(args: null, threads: 1, iterations: 1, report: true, concurrent: true, memoize: false, functionLatency: default(long), instrumentation: false, tag: null, loggingMethod: loggingMethod)
                .Execute(assertLatency: true) //measureLatency: false, report: true)
                .Verify();//report: true, listResults: false, expectedResults: default(IDictionary<string, object>), expectedMinimumLatency: 0L, expectedMaximumLatency: Int64.MaxValue, actualFunctionInvocationCounts: default(IDictionary<string, long>));

            Assert.That(LOG_LINES.Count, Is.EqualTo(2));
            Assert.That(LOG_LINES[0].StartsWith("PhasedExecutor: 1 round of {[1 concurrent thread, no args, latency unknown]} took "));
            Assert.That(LOG_LINES[0].Contains(" | "));
            Assert.That(LOG_LINES[0].EndsWith(" ticks"));
            Assert.That(LOG_LINES[1], Is.EqualTo("PhasedExecutor: 1 round of {[1 concurrent thread, no args, latency unknown]} expected results not given, internal latency boundaries demands: ..."));
        }





        // --- multiple iterations ---





        // --- multiple worker threads ---





        [Test]
        public void DefaultPhasedExecutionContext_AllIndividualResultsAreAvailable_ReportingIsOffByDefault(
            //[Values(1, 2, 100)] int iterations,
            [Values(1)] int iterations,
            [Values(1)] int threads
            )
        {
            int invocationCounter = 0;
            Func<int> myThreadSafeFunc = () => ++invocationCounter;

            TwoPhaseExecutionContext twoPhaseExecutionContext = myThreadSafeFunc
                .CreatePhasedExecutionContext(iterations: iterations, threads: threads, loggingMethod: loggingMethod)
                .Execute(measureLatency: false, assertLatency: false, report: true);

            Assert.That(LOG_LINES, Is.Empty);


            PhasedExecutionContextResult executionResults = twoPhaseExecutionContext.Results;
            Assert.That(executionResults, Is.Not.Null);
            Assert.That(executionResults.Count, Is.EqualTo(iterations));
            for (int i = 0; i < executionResults.Count; ++i)
            {
                //Assert.That(executionResults[i][0], Is.EqualTo(1)); // 1 == first invocationCounter count

                IList<object> iterationResults = twoPhaseExecutionContext.Results[i];
                Assert.That(iterationResults.Count, Is.EqualTo(threads));
                for (int j = 0; j < iterationResults.Count; ++j)
                {
                    Assert.That(iterationResults, Is.Not.Null);
                    Assert.That(iterationResults.Count, Is.EqualTo(threads));
                    Assert.That(iterationResults[j], Is.EqualTo(i + 1));
                }
            }
        }




        // --- context conjunctions ---





        [Test, ExpectedException(typeof(ApplicationException), ExpectedMessage = "No results are available", MatchType = MessageMatch.Exact)]
        public void ZeroThreadPhasedExecutionContext_ShouldGiveNotApplicableMessage_3()
        {
            long invocationCounter = 0;
            Func<long> myThreadSafeFunc = () => invocationCounter++;

            TwoPhaseExecutionContext twoPhaseExecutionContext = myThreadSafeFunc
                .CreatePhasedExecutionContext(iterations: 0, threads: 0, report: true, loggingMethod: loggingMethod)
                .Execute()
                .Verify();

            Assert.That(LOG_LINES.Count, Is.EqualTo(3));
            Assert.That(LOG_LINES[0], Is.EqualTo("PhasedExecutor: 0 rounds of {[0 concurrent threads, no args, latency unknown]} (no threads are declared - context is N/A)"));
            Assert.That(LOG_LINES[1], Is.EqualTo("PhasedExecutor: 0 rounds of {[0 concurrent threads, no args, ignore latency]} (no threads are declared - context is N/A)"));
            Assert.That(LOG_LINES[2], Is.EqualTo("PhasedExecutor: 0 rounds of {[0 concurrent threads, no args, ignore latency]}"));

            IEnumerable<object> results = twoPhaseExecutionContext.Results[0];
        }


        //[Test, ExpectedException(typeof(ApplicationException), ExpectedMessage = "No results are available", MatchType = MessageMatch.Exact)]
        //public void TestTarget_2_6()
        //{
        //    long invocationCounter = 0;
        //    Func<long> myThreadSafeFunc = () => invocationCounter++;

        //    TwoPhaseExecutionContext twoPhaseExecutionContext = myThreadSafeFunc.CreatePhasedExecutionContext(iterations: 0, threads: 0, loggingMethod: loggingMethod);
        //    twoPhaseExecutionContext.Execute(measureLatency: false, report: true);

        //    Assert.That(LOG_LINES.Count, Is.EqualTo(1));
        //    Assert.That(LOG_LINES[0], Is.EqualTo("PhasedExecutor: 0 rounds of {[0 concurrent threads, no args, ignore latency]} (context not yet executed)"));

        //    IEnumerable<object> results = twoPhaseExecutionContext.Results[0];
        //}


        [Test, ExpectedException(typeof(ApplicationException), ExpectedMessage = "No results are available", MatchType = MessageMatch.Exact)]
        public void ZeroThreadPhasedExecutionContext_QuietExecution()
        {
            long invocationCounter = 0;
            Func<long> myThreadSafeFunc = () => invocationCounter++;

            TwoPhaseExecutionContext twoPhaseExecutionContext = myThreadSafeFunc
                .CreatePhasedExecutionContext(iterations: 0, threads: 0, loggingMethod: loggingMethod)
                .Execute(measureLatency: false, report: false)
                .Verify(report: false, listResults: false);

            Assert.That(LOG_LINES, Is.Empty);

            IEnumerable<object> results = twoPhaseExecutionContext.Results[0];
        }


        [Test, ExpectedException(typeof(ApplicationException), ExpectedMessage = "No results are available", MatchType = MessageMatch.Exact)]
        public void ZeroThreadPhasedExecutionContext_VerificationReportOnly()
        {
            long invocationCounter = 0;
            Func<long> myThreadSafeFunc = () => invocationCounter++;

            TwoPhaseExecutionContext twoPhaseExecutionContext = myThreadSafeFunc.
                CreatePhasedExecutionContext(iterations: 0, threads: 0, loggingMethod: loggingMethod)
                .Execute(measureLatency: false, report: false)
                .Verify(report: true, listResults: false);

            Assert.That(LOG_LINES.Count, Is.EqualTo(1));
            Assert.That(LOG_LINES[0], Is.EqualTo("PhasedExecutor: 0 rounds of {[0 concurrent threads, no args, ignore latency]}"));

            IEnumerable<object> results = twoPhaseExecutionContext.Results[0];
        }


        [Test, ExpectedException(typeof(ApplicationException), ExpectedMessage = "No results are available", MatchType = MessageMatch.Exact)]
        public void ZeroThreadPhasedExecutionContext_VerificationReportOnly_WithEmptyResultListing()
        {
            long invocationCounter = 0;
            Func<long> myThreadSafeFunc = () => invocationCounter++;

            TwoPhaseExecutionContext twoPhaseExecutionContext = myThreadSafeFunc.CreatePhasedExecutionContext(iterations: 0, threads: 0, loggingMethod: loggingMethod);
            twoPhaseExecutionContext.Execute(measureLatency: false, report: false);
            twoPhaseExecutionContext.Verify(report: true, listResults: true);

            Assert.That(LOG_LINES.Count, Is.EqualTo(1));
            Assert.That(LOG_LINES[0], Is.EqualTo("PhasedExecutor: 0 rounds of {[0 concurrent threads, no args, ignore latency]}\r\nNo results available"));

            IEnumerable<object> results = twoPhaseExecutionContext.Results[0];
        }


        [Test, ExpectedException(typeof(ApplicationException), ExpectedMessage = "No results are available", MatchType = MessageMatch.Exact)]
        public void ZeroThreadPhasedExecutionContext_ExecutionAndVerificationReport_WithEmptyResultListing()
        {
            long invocationCounter = 0;
            Func<long> myThreadSafeFunc = () => invocationCounter++;

            TwoPhaseExecutionContext twoPhaseExecutionContext = myThreadSafeFunc.CreatePhasedExecutionContext(iterations: 0, threads: 0, loggingMethod: loggingMethod);
            twoPhaseExecutionContext.Execute(measureLatency: false, report: true);
            twoPhaseExecutionContext.Verify(report: true, listResults: true);

            Assert.That(LOG_LINES.Count, Is.EqualTo(2));
            Assert.That(LOG_LINES[0], Is.EqualTo("PhasedExecutor: 0 rounds of {[0 concurrent threads, no args, ignore latency]} (zero threads declared - context is N/A)"));
            Assert.That(LOG_LINES[1], Is.EqualTo("PhasedExecutor: 0 rounds of {[0 concurrent threads, no args, ignore latency]}\r\nNo results available"));

            IEnumerable<object> results = twoPhaseExecutionContext.Results[0];
        }


        [Test]//, ExpectedException(typeof(ApplicationException), ExpectedMessage = "No results are available", MatchType = MessageMatch.Exact)]
        public void ZeroThreadPhasedExecutionContext_WithLatencyMeasurements_ExecutionAndVerificationReport_WithEmptyResultListing()
        {
            long invocationCounter = 0;
            Func<long> myThreadSafeFunc = () => invocationCounter++;

            TwoPhaseExecutionContext twoPhaseExecutionContext = myThreadSafeFunc.CreatePhasedExecutionContext(iterations: 0, threads: 0, report: true, loggingMethod: loggingMethod);
            twoPhaseExecutionContext.Execute(measureLatency: true, assertLatency: true, report: true);
            twoPhaseExecutionContext.Verify(report: true, listResults: true);

            Assert.That(LOG_LINES.Count, Is.EqualTo(3));
            Assert.That(LOG_LINES[0], Is.EqualTo("PhasedExecutor: 0 rounds of {[0 concurrent threads, no args, latency unknown]} (zero threads declared - context is N/A)"));
            Assert.That(LOG_LINES[1], Is.EqualTo("PhasedExecutor: 0 rounds of {[0 concurrent threads, no args, ~12 us latency]} (zero threads declared - context is N/A)"));
            Assert.That(LOG_LINES[2], Is.EqualTo("PhasedExecutor: 0 rounds of {[0 concurrent threads, no args, ~12 us latency]} (should take [0, 24] us)\r\nNo results available"));

            IEnumerable<object> results = twoPhaseExecutionContext.Results[0];
        }


        [Test, ExpectedException(typeof(ApplicationException), ExpectedMessage = "No results are available", MatchType = MessageMatch.Exact)]
        public void TestTarget_2_0()
        {
            long invocationCounter = 0;
            Func<long> myThreadSafeFunc = () => invocationCounter++;

            TwoPhaseExecutionContext twoPhaseExecutionContext = myThreadSafeFunc.CreatePhasedExecutionContext(iterations: 0, threads: 0, report: true, loggingMethod: loggingMethod);
            twoPhaseExecutionContext.Execute(assertLatency: true, report: true);
            twoPhaseExecutionContext.Verify(report: true, listResults: true);



        }


        [Test, ExpectedException(typeof(ApplicationException), ExpectedMessage = "No results are available", MatchType = MessageMatch.Exact)]
        public void TestTarget_2_00()
        {
            long invocationCounter = 0;
            Func<long> myThreadSafeFunc = () => invocationCounter++;

            TwoPhaseExecutionContext twoPhaseExecutionContext = myThreadSafeFunc.CreatePhasedExecutionContext(iterations: 0, threads: 0, loggingMethod: loggingMethod);
            twoPhaseExecutionContext.Execute(measureLatency: true, assertLatency: true, report: true).Verify(report: true, listResults: true);
        }





        // TODO: merge with spec above...
        [Test]
        public void TwoPhaseExecutionContext_ZeroValuedParameters()
        {
            Func<string, long, string> myFunc = MemoizerTests.ReallySlowNetworkStaticInvocation;
            TwoPhaseExecutionContext twoPhaseExecutionContext = myFunc.CreateExecutionContext(iterations: 0, threads: 0, args: null, idempotent: true, functionLatency: 500, memoize: false);
            Console.WriteLine(twoPhaseExecutionContext);
        }


        [Test, ExpectedException(typeof(ArgumentException), ExpectedMessage = "Number-of-arguments parameter ('args') does not match the function signature", MatchType = MessageMatch.Exact)]
        public void TwoPhaseExecutionContext_NumberOfArgumentsParameterMustMatchTheFunctionSignature()
        {
            Func<string, long, string> myFunc = MemoizerTests.ReallySlowNetworkStaticInvocation;
            myFunc.CreateExecutionContext(args: new dynamic[] { "cowabunga" });
        }


        [Test, ExpectedException(typeof(InvalidOperationException), ExpectedMessage = "Execution context is not yet executed", MatchType = MessageMatch.Exact)]
        public void TwoPhaseExecutionContext_VerifyingAnUnExecutedContextShouldThrowException()
        {
            Func<string, long, string> myFunc = MemoizerTests.ReallySlowNetworkStaticInvocation;
            myFunc.CreateExecutionContext().Verify();
        }


        // TODO: merge with spec above...
        [Test]
        public void TwoPhaseExecutionContext_DefaultExecution()
        {
            Func<string, long, string> myFunc = MemoizerTests.ReallySlowNetworkStaticInvocation;
            myFunc.CreateExecutionContext().Execute().Verify();
            // TODO: asserts...?
        }


        [Test]
        public void TwoPhaseExecutionContext_Equals()
        {
            Func<string, long, string> func1 = MemoizerTests.ReallySlowNetworkStaticInvocation;
            TwoPhaseExecutionContext t1 = func1.CreateExecutionContext();
            Assert.That(t1, Is.EqualTo(t1));
            Assert.That(t1, Is.SameAs(t1));

            TwoPhaseExecutionContext t2 = func1.CreateExecutionContext(threads: 1);
            Assert.That(t1, Is.EqualTo(t2));
            Assert.That(t1, Is.Not.SameAs(t2));

            t2 = func1.CreateExecutionContext(threads: 2);
            Assert.That(t1, Is.Not.EqualTo(t2));

            t2 = func1.CreateExecutionContext(args: new object[] { "rihanna", 23L }, threads: 2);
            TwoPhaseExecutionContext t3 = func1.CreateExecutionContext(args: new object[] { "gaga", 23L }, threads: 2);
            Assert.That(t2, Is.Not.EqualTo(t3));


            Func<string, long, string> func2 = MemoizerTests.TypicalDatabaseStaticInvocation;
            TwoPhaseExecutionContext t4 = func2.CreateExecutionContext(threads: 1);
            Assert.That(t1, Is.Not.EqualTo(t4));

            t4 = func1.CreateExecutionContext(threads: 2);
            Assert.That(t1, Is.Not.EqualTo(t4));

        }


        [Test]
        public void TwoPhaseExecutionContext_GetExpectedFunctionInvocationCountFor()
        {
            Func<string, long, string> myFunc = MemoizerTests.ReallySlowNetworkStaticInvocation;
            TwoPhaseExecutionContext twoPhaseExecutionContext = myFunc.CreateExecutionContext();

            Assert.That(twoPhaseExecutionContext.GetExpectedFunctionInvocationCountFor(myFunc), Is.EqualTo(TwoPhaseExecutionContext.NUMBER_OF_WARM_UP_ITERATIONS + 1));
            twoPhaseExecutionContext.Having(iterations: 3);
            Assert.That(twoPhaseExecutionContext.GetExpectedFunctionInvocationCountFor(myFunc), Is.EqualTo(TwoPhaseExecutionContext.NUMBER_OF_WARM_UP_ITERATIONS + 3));
            twoPhaseExecutionContext.Having(iterations: 2);
            Assert.That(twoPhaseExecutionContext.GetExpectedFunctionInvocationCountFor(myFunc), Is.EqualTo(TwoPhaseExecutionContext.NUMBER_OF_WARM_UP_ITERATIONS + 2));

            const int THREADS = 5000;
            twoPhaseExecutionContext = myFunc.CreateExecutionContext(threads: THREADS);

            Assert.That(twoPhaseExecutionContext.GetExpectedFunctionInvocationCountFor(myFunc), Is.EqualTo(TwoPhaseExecutionContext.NUMBER_OF_WARM_UP_ITERATIONS + 1 * THREADS));
            twoPhaseExecutionContext.Having(iterations: 3);
            Assert.That(twoPhaseExecutionContext.GetExpectedFunctionInvocationCountFor(myFunc), Is.EqualTo(TwoPhaseExecutionContext.NUMBER_OF_WARM_UP_ITERATIONS + 3 * THREADS));
            twoPhaseExecutionContext.Having(iterations: 2);
            Assert.That(twoPhaseExecutionContext.GetExpectedFunctionInvocationCountFor(myFunc), Is.EqualTo(TwoPhaseExecutionContext.NUMBER_OF_WARM_UP_ITERATIONS + 2 * THREADS));

            twoPhaseExecutionContext = myFunc.CreateExecutionContext(threads: THREADS, memoize: true);

            Assert.That(twoPhaseExecutionContext.GetExpectedFunctionInvocationCountFor(myFunc), Is.EqualTo(TwoPhaseExecutionContext.NUMBER_OF_WARM_UP_ITERATIONS + 1));
            twoPhaseExecutionContext.Having(iterations: 3);
            Assert.That(twoPhaseExecutionContext.GetExpectedFunctionInvocationCountFor(myFunc), Is.EqualTo(TwoPhaseExecutionContext.NUMBER_OF_WARM_UP_ITERATIONS + 1));
            twoPhaseExecutionContext.Having(iterations: 2);
            Assert.That(twoPhaseExecutionContext.GetExpectedFunctionInvocationCountFor(myFunc), Is.EqualTo(TwoPhaseExecutionContext.NUMBER_OF_WARM_UP_ITERATIONS + 1));
        }


        [Test]
        public void TwoPhaseExecutionContext_MinimumExpextedLatencyInMilliseconds()
        {
            Func<string, long, string> myFunc = MemoizerTests.ReallySlowNetworkStaticInvocation;
            TwoPhaseExecutionContext twoPhaseExecutionContext = myFunc.CreateExecutionContext();
            Assert.That(twoPhaseExecutionContext.MinimumExpectedLatencyInMilliseconds, Is.EqualTo(default(long)));
            twoPhaseExecutionContext.Execute();
            Assert.That(twoPhaseExecutionContext.MinimumExpectedLatencyInMilliseconds, Is.GreaterThanOrEqualTo(MemoizerTests.NETWORK_RESPONSE_LATENCY_IN_MILLIS));
        }


        [Test]
        public void TwoPhaseExecutionContext_MaximumExpectedLatencyInMilliseconds()
        {
            Func<string, long, string> myFunc = MemoizerTests.ReallySlowNetworkStaticInvocation;
            TwoPhaseExecutionContext twoPhaseExecutionContext = myFunc.CreateExecutionContext();
            Assert.That(twoPhaseExecutionContext.MaximumExpectedLatencyInMilliseconds, Is.EqualTo(default(long)));
            twoPhaseExecutionContext.Execute();
            Assert.That(twoPhaseExecutionContext.MaximumExpectedLatencyInMilliseconds, Is.GreaterThan(MemoizerTests.NETWORK_RESPONSE_LATENCY_IN_MILLIS));
            Assert.That(twoPhaseExecutionContext.MaximumExpectedLatencyInMilliseconds, Is.LessThan(MemoizerTests.NETWORK_RESPONSE_LATENCY_IN_MILLIS + 200L));
        }


        [Test, ExpectedException(typeof(ArgumentException), ExpectedMessage = "TwoPhaseExecutionContext parameter cannot be null", MatchType = MessageMatch.Exact)]
        public void TwoPhaseExecutionContext_MergingWithNullShouldTerminateExecution()
        {
            Func<string, long, string> myFunc = MemoizerTests.ReallySlowNetworkStaticInvocation;
            TwoPhaseExecutionContext twoPhaseExecutionContext = myFunc.CreateExecutionContext();
            twoPhaseExecutionContext.And(null);
        }


        [Test]
        public void TwoPhaseExecutionContext_MergingWithItselfShouldGiveTheSameObject()
        {
            Func<string, long, string> myFunc = MemoizerTests.ReallySlowNetworkStaticInvocation;
            TwoPhaseExecutionContext twoPhaseExecutionContext = myFunc.CreateExecutionContext();
            TwoPhaseExecutionContext mergedContext = twoPhaseExecutionContext.And(twoPhaseExecutionContext);

            Assert.That(mergedContext.NumberOfIterations, Is.EqualTo(twoPhaseExecutionContext.NumberOfIterations));
            Assert.That(mergedContext.NumberOfFunctions, Is.EqualTo(twoPhaseExecutionContext.NumberOfFunctions));
            Assert.That(mergedContext.NumberOfConcurrentWorkerThreads, Is.EqualTo(twoPhaseExecutionContext.NumberOfConcurrentWorkerThreads));
            Assert.That(mergedContext, Is.EqualTo(twoPhaseExecutionContext));
            Assert.That(mergedContext, Is.SameAs(twoPhaseExecutionContext));
        }


        [Test]
        public void TwoPhaseExecutionContext_MergingWithEqualObjectShouldGiveTheSameObject()
        {
            Func<string, long, string> myFunc = MemoizerTests.ReallySlowNetworkStaticInvocation;
            TwoPhaseExecutionContext twoPhaseExecutionContext1 = myFunc.CreateExecutionContext();
            TwoPhaseExecutionContext twoPhaseExecutionContext2 = myFunc.CreateExecutionContext();
            TwoPhaseExecutionContext mergedContext = twoPhaseExecutionContext1.And(twoPhaseExecutionContext2);

            Assert.That(mergedContext, Is.EqualTo(twoPhaseExecutionContext1));
            Assert.That(mergedContext, Is.EqualTo(twoPhaseExecutionContext2));
            Assert.That(mergedContext, Is.SameAs(twoPhaseExecutionContext1));
            Assert.That(mergedContext, Is.Not.SameAs(twoPhaseExecutionContext2));

            mergedContext = twoPhaseExecutionContext2.And(twoPhaseExecutionContext1);

            Assert.That(mergedContext, Is.EqualTo(twoPhaseExecutionContext1));
            Assert.That(mergedContext, Is.EqualTo(twoPhaseExecutionContext2));
            Assert.That(mergedContext, Is.Not.SameAs(twoPhaseExecutionContext1));
            Assert.That(mergedContext, Is.SameAs(twoPhaseExecutionContext2));
        }





        [Test]
        public void TestTarget_1()
        {
            //actual    // expected
            //Assert.That(actual:1, expression:Is.EqualTo(2));

            //// Reset function 
            //Console.WriteLine(reallySlowNetworkInvocation1a_INVOCATION_COUNTER);
            //Interlocked.Exchange(ref reallySlowNetworkInvocation1a_INVOCATION_COUNTER, 0);
            //Console.WriteLine(reallySlowNetworkInvocation1a_INVOCATION_COUNTER);

            // Arrange context
            //TwoPhaseExecutionContextResultSet testResult =
            //this.reallySlowNetworkInvocation1a.CreateExecutionContext(threads: 100, args: new dynamic[] { "yo", 13 }, memoize: true, idempotentFunction: true)
            //    .And(this.reallySlowNetworkInvocation1a.CreateExecutionContext(threads: 90, args: new dynamic[] { "yoyo", 1313 }, memoize: true, idempotentFunction: true))
            //    .And(this.reallySlowNetworkInvocation1b.CreateExecutionContext(threads: 10, args: new dynamic[] { "yo", 13 }, memoize: true, idempotentFunction: true))
            //    .And(this.reallySlowNetworkInvocation1b.CreateExecutionContext(threads: 4, args: new dynamic[] { "yoyo", 1313 }, memoize: false, idempotentFunction: true))

            MemoizerTests memoizerTests = new MemoizerTests();

            TwoPhaseExecutionContext twoPhaseExecutionContext1 =
                memoizerTests.reallySlowNetworkInvocation1a.CreateExecutionContext(threads: 4,
                                                                                   args: new dynamic[] { "yo", 13 },
                                                                                   memoize: true,
                                                                                   instrumentation: false,
                                                                                   tag: "#1");
            TwoPhaseExecutionContext twoPhaseExecutionContext2 =
                memoizerTests.reallySlowNetworkInvocation1a.CreateExecutionContext(threads: 3,
                                                                                   args: new dynamic[] { "yoyo", 1313 },
                                                                                   memoize: false,
                                                                                   functionLatency: MemoizerTests.NETWORK_RESPONSE_LATENCY_IN_MILLIS + 100,
                                                                                   instrumentation: false,
                                                                                   tag: "#2");

            TwoPhaseExecutionContext twoPhaseExecutionContext = twoPhaseExecutionContext1.And(twoPhaseExecutionContext2);

            // Add additional _total_ execution context, e.g. number of iterations
            twoPhaseExecutionContext = twoPhaseExecutionContext.Having(iterations: 2);

            // Execute
            twoPhaseExecutionContext = twoPhaseExecutionContext.Execute(measureLatency: true, report: true);

            // Inject expected execution state, and verify execution
            IDictionary<string, object> results = new Dictionary<string, object>
            {
                { HashHelper.CreateFunctionHash(memoizerTests.reallySlowNetworkInvocation1a, "yoyo", 1313), "VeryExpensiveMethodResponseForyoyo1313" }, 
                { HashHelper.CreateFunctionHash(memoizerTests.reallySlowNetworkInvocation1a, "yo", 13), "VeryExpensiveMethodResponseForyo13" }
            };

            IDictionary<string, long> functionInvocationCounts = new Dictionary<string, long>
            {
                { HashHelper.CreateFunctionHash(memoizerTests.reallySlowNetworkInvocation1a), MemoizerTests.reallySlowNetworkInvocation1a_INVOCATION_COUNTER }
            };

            twoPhaseExecutionContext.Verify(expectedResults: results,
                                            expectedMinimumLatency: 0L,
                                            expectedMaximumLatency: twoPhaseExecutionContext.NumberOfIterations * MemoizerTests.NETWORK_RESPONSE_LATENCY_IN_MILLIS + 100,
                                            actualFunctionInvocationCounts: functionInvocationCounts // For memoizer testing mostly...
                                            );

            //TwoPhaseExecutionContext twoPhaseExecutionContext = twoPhaseExecutionContext1.And(twoPhaseExecutionContext2).Having(iterations: 1);
            ////TwoPhaseExecutionContext twoPhaseExecutionContext = twoPhaseExecutionContext2.And(twoPhaseExecutionContext1).Having(iterations: 1);

            //// Execute context
            ////TwoPhaseExecutionContextResultSet twoPhaseExecutionContextResultSet = twoPhaseExecutionContext1.Execute(report: true);
            ////TwoPhaseExecutionContextResultSet twoPhaseExecutionContextResultSet = twoPhaseExecutionContext2.Execute(report: true);
            //TwoPhaseExecutionContextResultSet twoPhaseExecutionContextResultSet = twoPhaseExecutionContext.Execute(report: true);


            //// Assert results of the executed functions

            ////TwoPhaseExecutionContextResult[] t = twoPhaseExecutionContextResultSet.ExecutionResult;
            ////Assert.That(t.Length, Is.EqualTo(1));
            ////TwoPhaseExecutionContextResult twoPhaseExecutionContextResult = t[0];
            ////DynamicTwoPhaseExecutorThread[] dynamicTwoPhaseExecutorThreads = twoPhaseExecutionContextResult.WorkerThreads;
            ////Assert.That(dynamicTwoPhaseExecutorThreads.Length, Is.EqualTo(1 + 1 * 2));
            ////DynamicTwoPhaseExecutorThread tt = dynamicTwoPhaseExecutorThreads[0];
            //////tt.Start(); // System.Threading.ThreadStateException : Thread is running or terminated; it cannot restart.

            ////var res_0_0 = twoPhaseExecutionContextResultSet[0, 0].Result;
            ////var res_0_1 = twoPhaseExecutionContextResultSet[0, 1].Result;
            ////var res_0_2 = twoPhaseExecutionContextResultSet[0, 2].Result;
            //////var res_0_3 = twoPhaseExecutionContextResultSet[0, 3].Result; // Exception: ...
            //////var res_1_0 = twoPhaseExecutionContextResultSet[1, 0].Result; // Exception: ...
            ////Assert.That(res_0_0, Is.EqualTo("VeryExpensiveMethodResponseForyo13"));
            ////Assert.That(res_0_1, Is.EqualTo("VeryExpensiveMethodResponseForyoyo1313"));
            ////Assert.That(res_0_2, Is.EqualTo("VeryExpensiveMethodResponseForyoyo1313"));

            //// New version:



            //// Assert latency/duration
            //long duration = twoPhaseExecutionContextResultSet.StopWatch.DurationInMilliseconds;
            //if (duration > twoPhaseExecutionContext.MaximumExpectedLatencyInMilliseconds)
            //    throw new ApplicationException("Memoizer.NET.TwoPhaseExecutor: Latency violation! [to slow...]");
            //if (duration < twoPhaseExecutionContext.MinimumExpextedLatencyInMilliseconds)
            //    throw new ApplicationException("Memoizer.NET.TwoPhaseExecutor: Latency violation! [too fast!?]");


            //// Assert number of invocations (when memoizing...)
            //// TODO: ...
            ////twoPhaseExecutionContext.PrintInvocationReport();

            ////Assert.That(reallySlowNetworkInvocation1a_INVOCATION_COUNTER, Is.EqualTo(twoPhaseExecutionContext1.GetExpectedFunctionInvocationCountFor(this.reallySlowNetworkInvocation1a /*, new dynamic[] { "yo", 13 }*/)));
            ////Assert.That(reallySlowNetworkInvocation1a_INVOCATION_COUNTER, Is.EqualTo(twoPhaseExecutionContext2.GetExpectedFunctionInvocationCountFor(this.reallySlowNetworkInvocation1a /*, new dynamic[] { "yo", 13 }*/)));
            //Assert.That(reallySlowNetworkInvocation1a_INVOCATION_COUNTER, Is.EqualTo(twoPhaseExecutionContext.GetExpectedFunctionInvocationCountFor(this.reallySlowNetworkInvocation1a /*, new dynamic[] { "yo", 13 }*/)));


            //// Assert ...

        }





        [Test, ExpectedException(typeof(ApplicationException), ExpectedMessage = "No results are available", MatchType = MessageMatch.Exact)]
        public void TestTarget_2_100()
        {
            long invocationCounter = 0;
            Func<long> myThreadSafeFunc = () => invocationCounter++;

            TwoPhaseExecutionContext twoPhaseExecutionContext = myThreadSafeFunc.CreatePhasedExecutionContext(report: true, loggingMethod: loggingMethod);

            Assert.That(LOG_LINES.Count, Is.EqualTo(1));
            Assert.That(LOG_LINES[0], Is.EqualTo("PhasedExecutor: 1 round of {[1 concurrent thread, no args, ignore latency]} (context not yet executed)"));

            IEnumerable<object> results = twoPhaseExecutionContext.Results[0];
        }





        [Test]
        public void TestTarget_3()
        {
            long invocationCounter = 0;
            Func<long> myThreadSafeFunc = () => invocationCounter++;

            TwoPhaseExecutionContext twoPhaseExecutionContext = myThreadSafeFunc.CreatePhasedExecutionContext(threads: 0, functionLatency: 1);
            twoPhaseExecutionContext.Execute(report: false);

            IEnumerable<object> results = twoPhaseExecutionContext.Results[0];

            Assert.That(results, Is.Not.Null);
            Assert.That(results, Is.Empty);
            Assert.That(results.Count(), Is.EqualTo(0));
            Assert.That(invocationCounter, Is.EqualTo(0));
        }


        [Test]
        public void TestTarget_4()
        {
            int invocationCounter = 0;

            Func<int> func = () => ++invocationCounter; // Not thread-safe
            //Func<int> func = new Func<int>(delegate()
            //    {
            //        Interlocked.Increment(ref invocationCounter);
            //        return invocationCounter;
            //    });

            IEnumerable<dynamic> results = func.CreatePhasedExecutionContext().Execute().Verify().Results[0];

            Assert.That(results.Count(), Is.EqualTo(1));
            Assert.That(results.ElementAtOrDefault(0), Is.EqualTo(1));
            Assert.That(invocationCounter, Is.EqualTo(1));

            object result = func.CreatePhasedExecutionContext(threads: 2).Execute().Verify().Results[0][0];
            Assert.That(result, Is.InRange(2, 3));
            Assert.That(invocationCounter, Is.EqualTo(3));

            result = func.CreatePhasedExecutionContext(threads: 2).Execute().Verify().Results[0][1];
            Assert.That(result, Is.InRange(4, 5));
            Assert.That(invocationCounter, Is.EqualTo(5));
        }


        static long INVOCATION_COUNTER;
        static readonly Func<decimal> FUNC = new Func<decimal>(delegate()
        {
            //Console.Write("FUNC() invoked... ");
            //Interlocked.Increment(ref reallySlowNetworkInvocation1a_INVOCATION_COUNTER);

            //Interlocked.Increment(ref INVOCATION_COUNTER);
            //++INVOCATION_COUNTER;
            decimal retVal = ++INVOCATION_COUNTER;
            //Thread.Yield();
            retVal *= 30m;
            //Thread.Sleep(10);
            //Thread.Yield();
            //Thread.SpinWait(43);
            retVal /= 2m;

            //Thread.Sleep(NETWORK_RESPONSE_LATENCY_IN_MILLIS);
            //Console.Write("FUNC() returns... ");
            return retVal;
        });

        [Test]
        public void TestTarget_5()
        {
            TwoPhaseExecutionContext context = FUNC.CreatePhasedExecutionContext(iterations: 3, threads: 400, functionLatency: 1);
            context = context.Execute();
            IList<object> results1 = context.Results[0];
            IList<object> results2 = context.Results[1];
            IList<object> results3 = context.Results[2];

            Assert.That(INVOCATION_COUNTER, Is.EqualTo(3 * 400));

            Assert.That(results1.Count(), Is.EqualTo(400));
            Assert.That(results2.Count(), Is.EqualTo(400));
            Assert.That(results3.Count(), Is.EqualTo(400));

            foreach (object result in results1) { Console.WriteLine(result); }

            Assert.That(results1, Contains.Item(15m));
            Assert.That(results1, Contains.Item(30m));
            Assert.That(results1, Contains.Item(45m));
            Assert.That(results1, Contains.Item(60m));
            // ...
        }





        // Not thread-safe version
        static int INVOCATION_COUNTER6;
        static readonly Func<int> FUNC6 = () => ++INVOCATION_COUNTER6;

        [Test]
        public void TestTarget_6(
            [Values(1)] int numberOfIterations,
            [Values(1, 2, 4)] int numberOfConcurrentWorkerThreads)
        {
            try
            {
                TwoPhaseExecutionContext context = FUNC6.CreatePhasedExecutionContext(iterations: numberOfIterations, threads: numberOfConcurrentWorkerThreads, functionLatency: 1).Execute();
                context.Verify();
                IList<object> results = context.Results[0];
                for (int j = 1; j <= numberOfConcurrentWorkerThreads; ++j)
                    Assert.That(results, Contains.Item(j));
            }
            finally
            {
                INVOCATION_COUNTER6 = 0;
            }
        }





        // Thread-safe version
        static int INVOCATION_COUNTER7;

        /// <summary>
        /// Lock object for removal of element and incrementing total element removal index.
        /// </summary>
        //static readonly object @lock = new Object();

        //static readonly Func<int> FUNC7 = new Func<int>(delegate()
        //{
        //    //lock (@lock)
        //    //{
        //    Interlocked.Increment(ref INVOCATION_COUNTER7);
        //    return INVOCATION_COUNTER7;
        //    //    return ++INVOCATION_COUNTER7;
        //    //}
        //});
        readonly Func<int> FUNC7 = delegate
            {
                Interlocked.Increment(ref INVOCATION_COUNTER7);
                return INVOCATION_COUNTER7;
            };

        [Test]
        public void TestTarget_7(
            [Values(4)] int numberOfIterations,
            [Values(1, 2, 4, 8, 12, 16, 20, 40, 80, 100, 200, 400, 800, 1000, 1200)] int numberOfConcurrentWorkerThreads)
        //[Values(50)] int numberOfConcurrentWorkerThreads)
        {
            try
            {
                TwoPhaseExecutionContext context =
                    FUNC7.CreatePhasedExecutionContext(iterations: numberOfIterations, threads: numberOfConcurrentWorkerThreads, functionLatency: 1)
                    .Execute()
                    .Verify();

                for (int i = 0; i < numberOfIterations; ++i)
                    for (int j = numberOfConcurrentWorkerThreads * numberOfIterations; j <= numberOfConcurrentWorkerThreads; ++j)
                        Assert.That(context.Results[i], Contains.Item(j));
            }
            finally
            {
                INVOCATION_COUNTER7 = 0;
            }
        }





        // TODO: ...
        [Ignore("Must be re-specified...")]
        [Test]//, ExpectedException(typeof(Exception), ExpectedMessage = "No TwoPhaseExecutionContextResults are available", MatchType = MessageMatch.Exact)]
        public void TwoPhaseExecutionContext_ZeroIterationParameter(
            [Values(0)] int numberOfIterations,
            [Values(1)] int numberOfConcurrentWorkerThreads)
        {
            //Func<string, long, string> myFunc = MemoizerTests.ReallySlowNetworkStaticInvocation;
            //TwoPhaseExecutionContext twoPhaseExecutionContext = myFunc.CreateExecutionContext(iterations: numberOfIterations, threads: numberOfConcurrentWorkerThreads);
            //// TODO: new API...
            /////*TwoPhaseExecutionContextResultSet twoPhaseExecutionContextResultSet =*/ twoPhaseExecutionContext.Execute("Jabadabadoo", 888L); // Hva med en liste med parametre, forskjellige for hver concurrent thread
            //////Assert.That(twoPhaseExecutionContextResultSet[0, 0], Is.Null);
            //TwoPhaseExecutionContextResultSet twoPhaseExecutionContextResultSet;
            //twoPhaseExecutionContext.Execute();//"Jabadabadoo", 888L); // Hva med en liste med parametre, forskjellige for hver concurrent thread - NOPE! :-)
            ////Assert.That(twoPhaseExecutionContextResultSet[0, 0], Is.Null);
            Func<string, long, string> myFunc = MemoizerTests.ReallySlowNetworkStaticInvocation;
            TwoPhaseExecutionContext twoPhaseExecutionContext = myFunc.CreateExecutionContext(iterations: numberOfIterations, threads: numberOfConcurrentWorkerThreads);
            // TODO: new API...
            ///*TwoPhaseExecutionContextResultSet twoPhaseExecutionContextResultSet =*/ twoPhaseExecutionContext.Execute("Jabadabadoo", 888L); // Hva med en liste med parametre, forskjellige for hver concurrent thread
            ////Assert.That(twoPhaseExecutionContextResultSet[0, 0], Is.Null);
            /*TwoPhaseExecutionContextResultSet twoPhaseExecutionContextResultSet =*/
            twoPhaseExecutionContext = twoPhaseExecutionContext.Execute();
            twoPhaseExecutionContext.Verify();
        }


        // TODO: ...
        [Ignore("Must be re-specified...")]
        [Test, ExpectedException(typeof(Exception), ExpectedMessage = "No FuncTwoPhaseExecutorThreads (worker threads) are available", MatchType = MessageMatch.Exact)]
        public void TwoPhaseExecutionContext_ZeroNumberOfConcurrentWorkerThreadsParameter(
            [Values(1)] int numberOfIterations,
            [Values(0)] int numberOfConcurrentWorkerThreads)
        {
            Func<string, long, string> myFunc = MemoizerTests.ReallySlowNetworkStaticInvocation;
            TwoPhaseExecutionContext twoPhaseExecutionContext = myFunc.CreateExecutionContext(iterations: numberOfIterations, threads: numberOfConcurrentWorkerThreads);
            // TODO: new API...
            ///*TwoPhaseExecutionContextResultSet twoPhaseExecutionContextResultSet =*/ twoPhaseExecutionContext.Execute("Jabadabadoo", 888L); // Hva med en liste med parametre, forskjellige for hver concurrent thread
            ////Assert.That(twoPhaseExecutionContextResultSet[0, 0], Is.Null);
        }


        // TODO: ...
        [Ignore("Must be re-specified...")]
        [Test, ExpectedException(typeof(Exception), ExpectedMessage = "Result set contains only 1 iteration... Really no point is asking for iteration #2 (zero-based) then, is it?", MatchType = MessageMatch.Exact)]
        public void TwoPhaseExecutionContext_OutOfBoundsErrorMessages1()
        {
            Func<string, long, string> myFunc = MemoizerTests.ReallySlowNetworkStaticInvocation;
            TwoPhaseExecutionContext twoPhaseExecutionContext = myFunc.CreateExecutionContext(iterations: 1, threads: 2);
            // TODO: new API...
            ///*TwoPhaseExecutionContextResultSet twoPhaseExecutionContextResultSet =*/ twoPhaseExecutionContext.Execute("Jabadabadoo", 888L); // Hva med en liste med parametre, forskjellige for hver concurrent thread
            ////var nonExistingIterationResultSet = twoPhaseExecutionContextResultSet[1, 0];
        }


        // TODO: ...
        [Ignore("Must be re-specified...")]
        [Test, ExpectedException(typeof(Exception), ExpectedMessage = "Result set contains only 3 iterations... Really no point is asking for iteration #20 (zero-based) then, is it?", MatchType = MessageMatch.Exact)]
        public void TwoPhaseExecutionContext_OutOfBoundsErrorMessages2()
        {
            Func<string, long, string> myFunc = MemoizerTests.ReallySlowNetworkStaticInvocation;
            TwoPhaseExecutionContext twoPhaseExecutionContext = myFunc.CreateExecutionContext(iterations: 3, threads: 2);
            // TODO: new API...
            ///*TwoPhaseExecutionContextResultSet twoPhaseExecutionContextResultSet = */twoPhaseExecutionContext.Execute("Jabadabadoo", 888L); // Hva med en liste med parametre, forskjellige for hver concurrent thread
            ////var nonExistingIterationResultSet = twoPhaseExecutionContextResultSet[19, 0];
        }


        // TODO: ...
        [Ignore("Must be re-specified...")]
        [Test, ExpectedException(typeof(Exception), ExpectedMessage = "Result set contains only 1 worker thread... Really no point is asking for thread #2 (zero-based) then, is it?", MatchType = MessageMatch.Exact)]
        public void TwoPhaseExecutionContext_OutOfBoundsErrorMessages3()
        {
            Func<string, long, string> myFunc = MemoizerTests.ReallySlowNetworkStaticInvocation;
            TwoPhaseExecutionContext twoPhaseExecutionContext = myFunc.CreateExecutionContext(iterations: 2, threads: 1);
            // TODO: new API...
            ///*TwoPhaseExecutionContextResultSet twoPhaseExecutionContextResultSet = */twoPhaseExecutionContext.Execute("Jabadabadoo", 888L); // Hva med en liste med parametre, forskjellige for hver concurrent thread
            ////var nonExistingWorkerThreadResultSet = twoPhaseExecutionContextResultSet[1, 1];
        }


        // TODO: ...
        [Ignore("Must be re-specified...")]
        [Test, ExpectedException(typeof(Exception), ExpectedMessage = "Result set contains only 10 worker threads... Really no point is asking for thread #100 (zero-based) then, is it?", MatchType = MessageMatch.Exact)]
        public void TwoPhaseExecutionContext_OutOfBoundsErrorMessages4()
        {
            Func<string, long, string> myFunc = MemoizerTests.ReallySlowNetworkStaticInvocation;
            TwoPhaseExecutionContext twoPhaseExecutionContext = myFunc.CreateExecutionContext(iterations: 3, threads: 10);
            // TODO: new API...
            ///*TwoPhaseExecutionContextResultSet twoPhaseExecutionContextResultSet =*/ twoPhaseExecutionContext.Execute("Jabadabadoo", 888L); // Hva med en liste med parametre, forskjellige for hver concurrent thread
            ////var nonExistingWorkerThreadResultSet = twoPhaseExecutionContextResultSet[1, 99];
        }
    }
}
