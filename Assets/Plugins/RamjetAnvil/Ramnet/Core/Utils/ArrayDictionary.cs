using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RamjetAnvil.Util {

    public class ArrayDictionary<TKey, TValue> : IDictionary<TKey, TValue> {

        private readonly IEqualityComparer<TValue> _comparer; 
        private readonly Func<TKey, int> _keyToIndex;
        private readonly Func<int, TKey> _indexToKey;
        private readonly bool[] _isSet;
        private readonly TValue[] _dict;

        public static ArrayDictionary<TKey, TValue> FromValues(Func<TKey, int> keyToIndex, Func<int, TKey> indexToKey, 
            params KeyValuePair<TKey, TValue>[] kvPairs) {

            int largestIndex = 0;
            for (int i = 0; i < kvPairs.Length; i++) {
                var kvPair = kvPairs[i];
                var index = keyToIndex(kvPair.Key);
                largestIndex = index > largestIndex ? index : largestIndex;
            }

            var dict = new ArrayDictionary<TKey, TValue>(keyToIndex, indexToKey, size: largestIndex + 1);
            for (int i = 0; i < kvPairs.Length; i++) {
                var kvPair = kvPairs[i];
                dict.Add(kvPair.Key, kvPair.Value);
            }
            return dict;
        } 

        public ArrayDictionary(Func<TKey, int> keyToIndex, Func<int, TKey> indexToKey, int size, IEqualityComparer<TValue> comparer = null) {
            _comparer = comparer ?? EqualityComparer<TValue>.Default;
            _keyToIndex = keyToIndex;
            _indexToKey = indexToKey;
            _isSet = new bool[size];
            _dict = new TValue[size];
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() {
            for (int i = 0; i < _dict.Length; i++) {
                if (_isSet[i]) {
                    yield return new KeyValuePair<TKey, TValue>(_indexToKey(i), _dict[i]);    
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public void Add(KeyValuePair<TKey, TValue> item) {
            var index = _keyToIndex(item.Key);
            _dict[_keyToIndex(item.Key)] = item.Value;
            _isSet[index] = true;
        }

        public void Clear() {
            for (int i = 0; i < _dict.Length; i++) {
                _dict[i] = default(TValue);
                _isSet[i] = false;
            }
        }

        public bool Contains(KeyValuePair<TKey, TValue> item) {
            var index = _keyToIndex(item.Key);
            var existingValue = _dict[index];
            return _isSet[index] && _comparer.Equals(existingValue, item.Value);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) {
            if (array == null) {
                throw new ArgumentNullException("array");
            }
            if (arrayIndex < 0) {
                throw new ArgumentOutOfRangeException("arrayIndex");
            }
            if (array.Length - arrayIndex < _dict.Length) {
                throw new ArgumentException("array is too small to copy all elements of this dictionary");
            }

            for (int i = 0; i < _dict.Length; i++) {
                if (_isSet[i]) {
                    array[arrayIndex] = new KeyValuePair<TKey, TValue>(_indexToKey(i), _dict[i]);
                    arrayIndex++;
                }
            }
        }

        public bool Remove(KeyValuePair<TKey, TValue> item) {
            var index = _keyToIndex(item.Key);
            var existingValue = _dict[index];
            if (_isSet[index] && _comparer.Equals(existingValue, item.Value)) {
                _dict[index] = default(TValue);
                _isSet[index] = false;
                return true;
            }
            return false;
        }

        public int Count {
            get { return _dict.Length; }
        }

        public bool IsReadOnly {
            get { return false; }
        }

        public void Add(TKey key, TValue value) {
            var index = _keyToIndex(key);
            _dict[index] = value;
            _isSet[index] = true;
        }

        public bool ContainsKey(TKey key) {
            return _isSet[_keyToIndex(key)];
        }

        public bool Remove(TKey key) {
            var index = _keyToIndex(key);
            if (_isSet[index]) {
                _dict[index] = default(TValue);
                _isSet[index] = false;
                return true;
            } 
            return false;
        }

        public bool TryGetValue(TKey key, out TValue value) {
            var index = _keyToIndex(key);
            if (_isSet[index]) {
                value = _dict[index];
                return true;
            }
            value = default(TValue);
            return false;
        }

        public TValue this[TKey key] {
            get { return _dict[_keyToIndex(key)]; }
            set {
                var index = _keyToIndex(key);
                _dict[index] = value;
                _isSet[index] = true;
            }
        }

        public ICollection<TKey> Keys {
            get {
                var keys = new TKey[_dict.Length];
                for (int i = 0; i < _dict.Length; i++) {
                    keys[i] = _indexToKey(i);
                }
                return keys;
            }
        }

        public ICollection<TValue> Values {
            get { return _dict; }
        }
    }
}
