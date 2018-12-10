#if UNITY_5 || UNITY_5_3_OR_NEWER
using System.Collections;
using Svelto.DataStructures;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Svelto.Tasks.Unity.Internal
{
    public static class UnityCoroutineRunner
    {
        public static void StopRoutines(FlushingOperation
            flushingOperation)
        {
            //note: _coroutines will be cleaned by the single tasks stopping silently. in this way they will be put
            //back to the pool. Let's be sure that the runner had the time to stop and recycle the previous tasks
            flushingOperation.stopped = true;
        }

        internal static void InitializeGameObject(string name, ref GameObject go, bool mustSurvive)
        {
            var taskRunnerName = "TaskRunner.".FastConcat(name);

            DBC.Tasks.Check.Require(GameObject.Find(taskRunnerName) == null, GAMEOBJECT_ALREADY_EXISTING_ERROR);

            go = new GameObject(taskRunnerName);

            if (mustSurvive && Application.isPlaying)
                Object.DontDestroyOnLoad(go);
        }

        internal class Process<RunningInfo> : IEnumerator where RunningInfo: IRunningTasksInfo
        {
            public Process( ThreadSafeQueue<IPausableTask> newTaskRoutines,
                            FasterList<IPausableTask>      coroutines, 
                            FlushingOperation              flushingOperation,
                            RunningInfo                    info)
            {
                _newTaskRoutines = newTaskRoutines;
                _coroutines = coroutines;
                _flushingOperation = flushingOperation;
                _info = info;
            }    

            public bool MoveNext()
            {
#if ENABLE_PLATFORM_PROFILER                
                using (var _platformProfiler = new Svelto.Common.PlatformProfiler(_info.runnerName))
#endif
                {
                    //don't start anything while flushing
                    if (_newTaskRoutines.Count > 0 && false == _flushingOperation.stopped) 
                        _newTaskRoutines.DequeueAllInto(_coroutines); 
                    
                    if (_coroutines.Count == 0 || _flushingOperation.paused == true) return true;

                    _info.Reset();
                    
                    int index = _flushingOperation.immediate == true ? _coroutines.Count - 1 : 0;

                    bool mustExit;
                    do
                    {
                        if (_info.CanProcessThis(ref index) == false) break;
                        
                        var coroutines = _coroutines.ToArrayFast();

                        bool result;
                        
                        if (_flushingOperation.stopped) coroutines[index].Stop();

#if ENABLE_PLATFORM_PROFILER
                        using (_platformProfiler.Sample(coroutines[index].ToString()))
#endif
                        {
#if TASKS_PROFILER_ENABLED
                            result =
 Svelto.Tasks.Profiler.TaskProfiler.MonitorUpdateDuration(coroutines[index], _info.runnerName);
#else
                            result = coroutines[index].MoveNext();
#endif
                        }
                        
                        var current = coroutines[index].Current;

                        if (result == false)
                            _coroutines.UnorderedRemoveAt(index);
                        else
                            index++;
                        
                        mustExit = (_coroutines.Count == 0 ||
                             _info.CanMoveNext(ref index, current) == false || index >= _coroutines.Count);
                    } 
                    while (!mustExit);
                }

                if (_flushingOperation.stopped == true && _coroutines.Count == 0)
                {   //once all the coroutines are flushed the loop can return accepting new tasks
                    _flushingOperation.stopped = false;
                }

                return true;
            }

            public void Reset()
            {}

            public object Current { get; private set; }
            
            readonly ThreadSafeQueue<IPausableTask> _newTaskRoutines;
            readonly FasterList<IPausableTask>      _coroutines;
            readonly FlushingOperation              _flushingOperation;
            
            RunningInfo _info;
        }

        public struct RunningTasksInfo:IRunningTasksInfo
        {
            public bool CanMoveNext(ref int nextIndex, TaskCollection<IEnumerator>.CollectionTask currentResult)
            {
                return true;
            }

            public bool CanProcessThis(ref int index)
            {
                return true;
            }

            public void Reset()
            {}

            public string runnerName { get; set; }
        }

        public class FlushingOperation
        {
            public bool paused;
            public bool stopped;
            public bool immediate;
        }

        const string GAMEOBJECT_ALREADY_EXISTING_ERROR = "A MonoRunner GameObject with the same name was already been used, did you forget to dispose the old one?";
    }

    public interface IRunningTasksInfo
    {
        bool CanMoveNext(ref int nextIndex, TaskCollection<IEnumerator>.CollectionTask currentResult);
        bool CanProcessThis(ref int index);
        void Reset();
        string runnerName { get; }
    }
}
#endif