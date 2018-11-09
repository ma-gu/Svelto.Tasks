#if UNITY_5 || UNITY_5_3_OR_NEWER

using Svelto.DataStructures;
using Svelto.Tasks.Unity.Internal;
using UnityEngine;

#if TASKS_PROFILER_ENABLED
using Svelto.Tasks.Profiler;
#endif

namespace Svelto.Tasks.Unity
{
    /// <summary>
    /// while you can instantiate a MonoRunner, you should use the standard one
    /// whenever possible. Instantiating multiple runners will defeat the
    /// initial purpose to get away from the Unity monobehaviours
    /// internal updates. MonoRunners are disposable though, so at
    /// least be sure to dispose of them once done
    /// </summary>

    public abstract class MonoRunner : IRunner
    {
        public bool paused { set; get; }
        public bool isStopping { get { return _flushingOperation.stopped; } }
        public bool isKilled { get {return _go == null;} }
        public int  numberOfRunningTasks { get { return _coroutines.Count; } }
        
        public GameObject _go;

        ~MonoRunner()
        {
            StopAllCoroutines();
        }
        
        /// <summary>
        /// TaskRunner doesn't stop executing tasks between scenes
        /// it's the final user responsibility to stop the tasks if needed
        /// </summary>
        public virtual void StopAllCoroutines()
        {
            paused = false;

            UnityCoroutineRunner.StopRoutines(_flushingOperation);

            _newTaskRoutines.Clear();
        }

        public virtual void StartCoroutine(IPausableTask task)
        {
            paused = false;

            _newTaskRoutines.Enqueue(task); //careful this could run on another thread!
        }

        public virtual void Dispose()
        {
            StopAllCoroutines();
            
            GameObject.DestroyImmediate(_go);
            _go = null;
        }
        
        protected readonly ThreadSafeQueue<IPausableTask> _newTaskRoutines = new ThreadSafeQueue<IPausableTask>();
        protected readonly FasterList<IPausableTask> _coroutines =
            new FasterList<IPausableTask>(NUMBER_OF_INITIAL_COROUTINE);
        
        protected UnityCoroutineRunner.FlushingOperation _flushingOperation =
            new UnityCoroutineRunner.FlushingOperation();
        
        const int NUMBER_OF_INITIAL_COROUTINE = 3;
    }
}
#endif
