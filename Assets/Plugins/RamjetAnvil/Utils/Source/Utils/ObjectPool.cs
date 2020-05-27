using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RamjetAnvil.RamNet;

namespace RamjetAnvil.Util {

    public interface IObjectPool<T> {
        IPooledObject<T> Take();
    }

    public interface IBasicObjectPool<T> {
        T Take();
        void Return(T pooledObject);
    }

    public interface IPooledObject<out T> : IDisposable {
        T Instance { get; }
    }

    public class BasicObjectPool<T> : IBasicObjectPool<T> {

        public const int DefaultGrowthStep = 4;

        private readonly int _growthStep;
        private readonly Action<T> _onReturnToPool;
        private readonly Action<T> _onTakenFromPool;
        private readonly Func<IBasicObjectPool<T>, T> _construct;
        private readonly Stack<T> _pool;

        public BasicObjectPool(Func<IBasicObjectPool<T>, T> construct) :
            this(construct, null, null, DefaultGrowthStep) {}

        public BasicObjectPool(Func<IBasicObjectPool<T>, T> construct, int growthStep) : this(construct, null, null, growthStep) {}

        public BasicObjectPool(Func<IBasicObjectPool<T>, T> construct, Action<T> onReturnToPool) :
            this(construct, onReturnToPool, null, DefaultGrowthStep) {}

        public BasicObjectPool(Func<IBasicObjectPool<T>, T> construct, Action<T> onReturnToPool, Action<T> onTakenFromPool) :
            this(construct, onReturnToPool, onTakenFromPool, DefaultGrowthStep) {}

        public BasicObjectPool(Func<IBasicObjectPool<T>, T> construct,
            Action<T> onReturnToPool,
            Action<T> onTakenFromPool,
            int growthStep) {

            _growthStep = growthStep;
            _onReturnToPool = onReturnToPool ?? (_ => {});
            _onTakenFromPool = onTakenFromPool ?? (_ => {});
            _construct = construct;
            _pool = new Stack<T>(growthStep);
            GrowPool();
        }

        public T Take() {
            if (_pool.Count == 0) {
                GrowPool();
            }
            var instance = _pool.Pop();
            _onTakenFromPool(instance);
            return instance;
        }

        public void Return(T pooledObject) {
            AddToPool(@object: pooledObject);
        }

        private void GrowPool() {
            for (int i = 0; i < _growthStep; i++) {
                AddToPool(@object: _construct(this));
            }
        }

        private void AddToPool(T @object) {
            _onReturnToPool(@object);
            _pool.Push(@object);
        }
    }

    public class ManagedObject<T> {
        private readonly T _instance;
        private readonly Action _onReturnedToPool;
        private readonly Action _onTakenFromPool;

        public ManagedObject(T instance) {
            _instance = instance;
            _onReturnedToPool = () => { };
            _onTakenFromPool = () => { };
        }

        public ManagedObject(T instance, Action onReturnedToPool) {
            _instance = instance;
            _onReturnedToPool = onReturnedToPool;
            _onTakenFromPool = () => { };
        }

        public ManagedObject(T instance, Action onReturnedToPool, Action onTakenFromPool) {
            _instance = instance;
            _onReturnedToPool = onReturnedToPool;
            _onTakenFromPool = onTakenFromPool;
        }

        public T Instance {
            get { return _instance; }
        }

        public Action OnReturnedToPool {
            get { return _onReturnedToPool; }
        }

        public Action OnTakenFromPool {
            get { return _onTakenFromPool; }
        }
    }

    public class UnmanagedObject<T> : IPooledObject<T> {

        private readonly T _instance;

        public UnmanagedObject(T instance) {
            _instance = instance;
        }

        public void Dispose() {
            // Does nothing
        }

        public T Instance {
            get { return _instance; }
        }
    }

    public class ObjectPool<T> : IObjectPool<T> {

        private readonly IBasicObjectPool<PooledObject> _baseObjectPool;

        public ObjectPool(Func<ManagedObject<T>> factory) : this(factory, BasicObjectPool<T>.DefaultGrowthStep) {}

        public ObjectPool(Func<T> factory, Action<T> onReturnedToPool = null, int growthStep = 10) 
            : this(() => {
                var instance = factory();
                return new ManagedObject<T>(
                    instance,
                    () => {
                        if (onReturnedToPool != null) {
                            onReturnedToPool(instance);
                        }
                    });
            }, BasicObjectPool<T>.DefaultGrowthStep) {}

        public ObjectPool(Func<ManagedObject<T>> factory, int growthStep) {
            Action<PooledObject> returnToPool = pooledObject => _baseObjectPool.Return(pooledObject);
            _baseObjectPool = new BasicObjectPool<PooledObject>(
                pool => new PooledObject(returnToPool, @object: factory()),
                onReturnToPool: @object => @object.ManagedObject.OnReturnedToPool(),
                onTakenFromPool: @object => @object.ManagedObject.OnTakenFromPool(),
                growthStep: growthStep);
        }

        public IPooledObject<T> Take() {
            var pooledObject = _baseObjectPool.Take();
            pooledObject.ManagedObject.OnTakenFromPool();
            return pooledObject;
        }

        private class PooledObject : IPooledObject<T> {

            private readonly Action<PooledObject> _returnToPool;
            private readonly ManagedObject<T> _object;

            public PooledObject(Action<PooledObject> returnToPool, ManagedObject<T> @object) {
                _returnToPool = returnToPool;
                _object = @object;
            }

            public ManagedObject<T> ManagedObject {
                get { return _object; }
            }

            public T Instance {
                get {
                    return _object.Instance;
                }
            }

            public void Dispose() {
                _returnToPool(this);
            }
        }
    }

    public static class ObjectPool {
        public static IObjectPool<T> FromResetable<T>(Func<T> factory, int growthStep) where T : IResetable {
            return new ObjectPool<T>(() => {
                var o = factory();
                return new ManagedObject<T>(o, o.Reset);
            },
            growthStep);
        } 
    }
}
