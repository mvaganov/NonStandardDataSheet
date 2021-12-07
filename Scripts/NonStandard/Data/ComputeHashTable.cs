using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace NonStandard.Data {
	/// <summary>
	/// hash table that can have callbacks put on key/value pairs to notify when values change (keys are immutable)
	/// key/value pairs can also be Set to functions, meaning that they are calculated by code, including possibly other members of this dictionary, which could also be functions. dependencies are calculated, and errors are thrown if recursion is discovered.
	/// </summary>
	/// <typeparam name="KEY"></typeparam>
	/// <typeparam name="VAL"></typeparam>
	public class ComputeHashTable<KEY, VAL> : IDictionary<KEY, VAL> where KEY : IComparable<KEY> {
		/// <summary>
		/// user can define their own hash function
		/// </summary>
		public HashFunction_t hFunc = null;
		/// <summary>
		/// callback whenever any change is made. onChange(key, oldValue, newValue)
		/// </summary>
		public KeyValueChangeCallback onChange;
		protected List<List<KV>> buckets;

		public delegate void KeyValueChangeCallback(KEY Key, VAL oldValue, VAL newValue);
		public delegate int HashFunction_t(KEY Key);

		public const int defaultBuckets = 8;
		public const int maxComputeDepth = 1000;
		/// <summary>
		/// used to log the order of key/value pairs and cache them in an easily traversed list
		/// </summary>
		protected List<KV> orderedPairs = new List<KV>();
		// TODO prototype fallback dictionary, like EcmaScript
		public enum ResultOfAssigningToFunction { ThrowException, Ignore, OverwriteFunction }
		public ResultOfAssigningToFunction onAssignmentToFunction = ResultOfAssigningToFunction.ThrowException;
		public void FunctionAssignIgnore() { onAssignmentToFunction = ResultOfAssigningToFunction.Ignore; }
		public void FunctionAssignException() { onAssignmentToFunction = ResultOfAssigningToFunction.ThrowException; }
		public void FunctionAssignOverwrite() { onAssignmentToFunction = ResultOfAssigningToFunction.OverwriteFunction; }
		int Hash(KEY key) { return Math.Abs(hFunc != null ? hFunc(key) : key.GetHashCode()); }
		public class KV : Computable<KEY,VAL> {
			public readonly int hash;
			public KV(int hash, KEY k) : this(hash, k, default(VAL)) { }
			public KV(int h, KEY k, VAL v) : base(k,v) { hash = h; }
			public override string ToString() { return key + "(" + hash + "):" + val; }
			public class Comparer : IComparer<KV> {
				public int Compare(KV x, KV y) { return x.hash.CompareTo(y.hash); }
			}
			public static Comparer comparer = new Comparer();
			public static implicit operator KeyValuePair<KEY, VAL>(KV k) { return new KeyValuePair<KEY, VAL>(k.key, k.val); }
		}
		private KV Kv(KEY key) { return new KV(Hash(key), key); }
		private KV Kv(KEY key, VAL val) { return new KV(Hash(key), key, val); }
		public ComputeHashTable(HashFunction_t hashFunc, int bCount = defaultBuckets) { hFunc = hashFunc; BucketCount = bCount; }
		public ComputeHashTable() { }
		public ComputeHashTable(int bucketCount) { BucketCount = bucketCount; }
		public int Count {
			get {
				int sum = 0;
				if (buckets != null) { buckets.ForEach(bucket => sum += bucket != null ? bucket.Count : 0); }
				return sum;
			}
		}
		public int BucketCount { get { return buckets != null ? buckets.Count : 0; } set { SetHashFunction(hFunc, value); } }
		public HashFunction_t HashFunction { get { return hFunc; } set { SetHashFunction(value, BucketCount); } }
		public void SetHashFunction(HashFunction_t hFunc, int bucketCount) {
			this.hFunc = hFunc;
			if (bucketCount <= 0) { buckets = null; return; }
			SetBucketCount(bucketCount);
		}
		protected void SetBucketCount(int bucketCount) {
			List<List<KV>> oldbuckets = buckets;
			buckets = new List<List<KV>>(bucketCount);
			for (int i = 0; i < bucketCount; ++i) { buckets.Add(null); }
			if (oldbuckets != null) {
				oldbuckets.ForEach(b => { if (b != null) b.ForEach(kvp => Set(kvp.key, kvp.val)); });
			}
		}
		protected int FindExactIndex(KV kvp, int index, List<KV> list) {
			while (index > 0 && list[index - 1].hash == kvp.hash) { --index; }
			do {
				int compareValue = list[index].key.CompareTo(kvp.key);
				if (compareValue == 0) return index;
				if (compareValue > 0) return ~index;
				++index;
			} while (index < list.Count && list[index].hash == kvp.hash);
			return ~index;
		}
		protected void EnsureBuckets() {
			if (buckets == null || buckets.Count == 0) { SetBucketCount(defaultBuckets); }
		}
		protected void FindEntry(KV kvp, out List<KV> bucket, out int bestIndexInBucket) {
			EnsureBuckets();
			int whichBucket = kvp.hash % buckets.Count;
			bucket = buckets[whichBucket];
			if (bucket == null) { buckets[whichBucket] = bucket = new List<KV>(); }
			bestIndexInBucket = bucket.BinarySearch(kvp, KV.comparer);
			if (bestIndexInBucket < 0) { return; }
			bestIndexInBucket = FindExactIndex(kvp, bestIndexInBucket, bucket);
		}
		public bool Set(KEY key, VAL val) { return Set(Kv(key, val)); }
		public bool Set(KV kvp) {
			EnsureBuckets();
			List<KV> bucket; int bestIndexInBucket;
			FindEntry(kvp, out bucket, out bestIndexInBucket);
			bool inserted = false;
			if (bestIndexInBucket < 0) {
				bestIndexInBucket = ~bestIndexInBucket;
				bucket.Insert(bestIndexInBucket, kvp);
				orderedPairs.Add(kvp);
				inserted = true;
			}
			SetValue_Internal(bucket[bestIndexInBucket], kvp.val);
			return inserted;
		}
		protected void SetValue_Internal(KV dest, VAL value) {
			if (dest.IsComputed) {
				switch (onAssignmentToFunction) {
				case ResultOfAssigningToFunction.ThrowException:
					string errorMessage = "can't set " + dest.key + ", this value is computed.";
					if (dest.reliesOn != null) {
						errorMessage += " relies on: " + string.Join(", ", dest.reliesOn.ConvertAll(kv => kv.key.ToString()).ToArray());
					}
					throw new Exception(errorMessage);
				case ResultOfAssigningToFunction.Ignore: return;
				case ResultOfAssigningToFunction.OverwriteFunction: dest.Compute = null; break;
				}
			}
			VAL old = dest.val;
			dest.SetInternal(value);
			onChange?.Invoke(dest._key, old, dest._val);
		}
		public bool Set(KEY key, Func<VAL> valFunc) {
			EnsureBuckets();
			List<KV> bucket; int bestIndexInBucket;
			KV kvp = Kv(key);
			FindEntry(kvp, out bucket, out bestIndexInBucket);
			if (bestIndexInBucket < 0) {
				bestIndexInBucket = ~bestIndexInBucket;
				bucket.Insert(bestIndexInBucket, kvp);
				orderedPairs.Add(kvp);
			}
			bucket[bestIndexInBucket].Compute = valFunc;
			return true;
		}
		public bool TryGet(KEY key, out KV entry) {
			entry = Kv(key);
			if (buckets == null || buckets.Count == 0) return false;
			List<KV> bucket; int bestIndexInBucket;
			FindEntry(entry, out bucket, out bestIndexInBucket);
			if (bestIndexInBucket >= 0) {
				entry = bucket[bestIndexInBucket];
				return true;
			}
			return false;
		}
		public VAL Get(KEY key) {
			KV kvPair;
			if (TryGet(key, out kvPair)) { return kvPair.val; }
			throw new Exception("map does not contain key '"+key+"'");
		}
		/// <summary>
		/// calls any change listeners to mark initialization
		/// </summary>
		public void NotifyStart() {
			if (onChange != null) { onChange.Invoke(default(KEY), default(VAL), default(VAL)); }
		}
		public string Show(bool showCalcualted) {
			StringBuilder sb = new StringBuilder();
			bool printed = false;
			for (int i = 0; i < orderedPairs.Count; ++i) {
				if (showCalcualted || orderedPairs[i].Compute == null) {
					if (printed) sb.Append("\n");
					sb.Append(orderedPairs[i].ToString(true, false));
					printed = true;
				}
			}
			return sb.ToString();
		}
/////////////////////////////////////////////// implementing IDictionary below ////////////////////////////////////////
		public ICollection<KEY> Keys { get { return orderedPairs.ConvertAll(kv => kv.key); } }
		public ICollection<VAL> Values { get { return orderedPairs.ConvertAll(kv => kv.val); } }
		public bool IsReadOnly { get { return false; } }
		public VAL this[KEY key] { get { return Get(key); } set { Set(key, value); } }
		public void Add(KEY key, VAL value) { Set(key, value); }
		public bool ContainsKey(KEY key) {
			List<KV> bucket;  int bestIndexInBucket;
			KV kv = Kv(key);
			FindEntry(kv, out bucket, out bestIndexInBucket);
			return bestIndexInBucket >= 0;
		}
		public bool Remove(KEY key) {
			List<KV> bucket; int bestIndexInBucket;
			FindEntry(Kv(key), out bucket, out bestIndexInBucket);
			if (bestIndexInBucket >= 0) {
				orderedPairs.Remove(bucket[bestIndexInBucket]);
				bucket.RemoveAt(bestIndexInBucket);
				return true;
			}
			return false;
		}
		public bool TryGetValue(KEY key, out VAL value) {
			KV found; if (TryGet(key, out found)) { value = found.val; return true; }
			value = default(VAL); return false;
		}
		public void Add(KeyValuePair<KEY, VAL> item) { Set(Kv(item.Key, item.Value)); }
		public void Clear() {
			if (buckets == null) return;
			for (int i = 0; i < buckets.Count; ++i) { if(buckets[i] != null) buckets[i].Clear(); }
			orderedPairs.Clear();
		}
		public bool Contains(KeyValuePair<KEY, VAL> item) {
			List<KV> bucket; int bestIndex;
			FindEntry(Kv(item.Key), out bucket, out bestIndex);
			return bestIndex >= 0 && bucket[bestIndex].val.Equals(item.Value);
		}
		public void CopyTo(KeyValuePair<KEY, VAL>[] array, int arrayIndex) {
			int index = arrayIndex;
			for (int i = 0; i < orderedPairs.Count; ++i) { array[index++] = orderedPairs[i]; }
		}
		public bool Remove(KeyValuePair<KEY, VAL> item) {
			List<KV> bucket; int bestIndex;
			FindEntry(Kv(item.Key), out bucket, out bestIndex);
			if (bestIndex >= 0 && item.Value.Equals(bucket[bestIndex].val)) {
				orderedPairs.Remove(bucket[bestIndex]);
				bucket.RemoveAt(bestIndex);
				return true;
			}
			return false;
		}
		public IEnumerator<KeyValuePair<KEY, VAL>> GetEnumerator() { return new Enumerator(this); }
		IEnumerator IEnumerable.GetEnumerator() { return new Enumerator(this); }
		public class Enumerator : IEnumerator<KeyValuePair<KEY, VAL>> {
			ComputeHashTable<KEY, VAL> htable;
			int index = -1; // MoveNext() is always called before the enumeration begins, to see if any values exist
			public Enumerator(ComputeHashTable<KEY, VAL> htable) { this.htable = htable; }
			public KeyValuePair<KEY, VAL> Current { get { return htable.orderedPairs[index]; } }
			object IEnumerator.Current { get { return Current; } }
			public void Dispose() { htable = null; }
			public bool MoveNext() {
				if (htable.orderedPairs == null || index >= htable.orderedPairs.Count) return false;
				return ++index < htable.orderedPairs.Count;
			}
			public void Reset() { index = -1; }
		}
	}
}