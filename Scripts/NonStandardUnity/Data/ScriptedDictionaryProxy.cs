using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NonStandard.Data {
	public class ScriptedDictionaryProxy : MonoBehaviour, IDictionary<string, object> {
		public GameObject dictionary;
		private IDictionary<string, object> _dict;
		public IDictionary<string, object> Dict => _dict != null ? _dict : _dict = dictionary.GetComponent<ScriptedDictionary>();
		public void SetDictionary(IDictionary<string, object> dict) { _dict = dict; }
		public object this[string key] { get => Dict[key]; set => Dict[key] = value; }
		public ICollection<string> Keys => Dict.Keys;
		public ICollection<object> Values => Dict.Values;
		public int Count => Dict.Count;
		public bool IsReadOnly => Dict.IsReadOnly;
		public void Add(string key, object value) => Dict.Add(key, value);
		public void Add(KeyValuePair<string, object> item) => Dict.Add(item);
		public void Clear() =>_dict.Clear();
		public bool Contains(KeyValuePair<string, object> item) => Dict.Contains(item);
		public bool ContainsKey(string key) => _dict.ContainsKey(key);
		public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex) => Dict.CopyTo(array, arrayIndex);
		public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => Dict.GetEnumerator();
		public bool Remove(string key) => Dict.Remove(key);
		public bool Remove(KeyValuePair<string, object> item) => Dict.Remove(item);
		public bool TryGetValue(string key, out object value) => Dict.TryGetValue(key, out value);
		IEnumerator IEnumerable.GetEnumerator() => Dict.GetEnumerator();
	}
}