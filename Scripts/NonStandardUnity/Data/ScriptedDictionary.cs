//#define TEST
using NonStandard.Data.Parse;
using NonStandard.Extension;
using NonStandard.Process;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace NonStandard.Data {

	[System.Serializable, StringifyHideType]
	public class HashTable_stringobject : ComputeHashTable<string, object> { }
	public class ScriptedDictionary : MonoBehaviour, IDictionary<string, object>
	{
		[SerializeField, HideInInspector] protected HashTable_stringobject dict = new HashTable_stringobject();
		[TextArea(3, 10)]
		public string values;
#if UNITY_EDITOR
		[TextArea(1, 10)]
		public string parseResults;

		bool validating = false;
		void OnValidate() {
			Tokenizer tok = new Tokenizer();
			if (Application.isPlaying) {
				CodeConvert.TryFill(values, ref dict, null, tok);
			} else {
				CodeConvert.TryParse(values, out dict, null, tok);
			}
			if (tok.errors.Count > 0) {
				parseResults = tok.errors.JoinToString("\n");
			} else {
				//parseResults = dict.Show(true);
				string newResults = dict.Stringify(pretty:true,showType:true,showNulls:true);
				if (parseResults != newResults) { parseResults = newResults; }
			}
		}
		public void GiveInventoryTo(TMPro.TMP_Text textElement) {
			string outText = dict.Stringify(true);
			Debug.Log(outText);
			textElement.text = outText;
		}
#endif
		public void UpdateValueStringWithDictionary() {
#if UNITY_EDITOR
			if (!validating) {
				validating = true;
#endif
				// TODO make this work better... currently giving strings without quotes
				Proc.Delay(0, () => { values = dict.Show(true); });
#if UNITY_EDITOR
			}
#endif
		}
		public StringEvent dictionaryTostringChangeListener;
		public ScriptedDictionary GetVars(string name) {
			ScriptedDictionaryManager m = Global.Get<ScriptedDictionaryManager>();
			if (m == null) return null;
			return m.Find(other => other.name == name);
		}

		[System.Serializable] public class StringEvent : UnityEvent<string> { }

		void Awake() {
			Global.GetComponent<ScriptedDictionaryManager>().Register(this);
			if (!string.IsNullOrEmpty(values)) {
				Tokenizer tok = new Tokenizer();
				CodeConvert.TryParse(values, out dict, null, tok);
				if (tok.HasError()) { Show.Warning(tok.GetErrorString()); }
			}
		}
		void Start() {
#if UNITY_EDITOR
			dict.onChange += (k, a, b) => { UpdateValueStringWithDictionary(); };
#endif
			dict.onChange += (k, a, b) => {
				if (dictionaryTostringChangeListener.GetPersistentEventCount() > 0) {
					string s = dict.Stringify(true);
					//Debug.Log(s);
					dictionaryTostringChangeListener.Invoke(s);
				}
			};
			dict.FunctionAssignIgnore();
#if TEST
			string[] mainStats = new string[] { "str", "con", "dex", "int", "wis", "cha" };
			int[] scores = { 8, 8, 18, 12, 9, 14 };
			for (int i = 0; i < mainStats.Length; ++i) {
				dict[mainStats[i]] = scores[i];
			}
			for (int i = 0; i < mainStats.Length; ++i) {
				string s = mainStats[i];
				dict.Set("_" + s, () => CalcStatModifier(s));
			}
			AddTo("cha", 4);
#endif
			dict.NotifyStart();
		}
#if TEST
		private int CalcStatModifier(string s) {
			return (int)Mathf.Floor((NumValue(s) - 10) / 2);
		}
#endif

		public float NumValue(string fieldName) {
			object val;
			if (!dict.TryGetValue(fieldName, out val)) return 0;
			CodeConvert.TryConvert(ref val, typeof(float));
			return (float)val;
		}
		public void AddTo(string fieldName, float bonus) {
			dict[fieldName] = NumValue(fieldName) + bonus;
			//Show.Log("'" + fieldName + "' += " + bonus + " (" + dict[fieldName] + ") " + ReflectionExtension.GetStack(6));
		}
		public string Format(string text) {
			Tokenizer tok = new Tokenizer();
			string resolvedText = CodeConvert.Format(text, dict, tok);
			if (tok.HasError()) {
				Show.Error(tok.errors.JoinToString(", "));
			}
			return resolvedText;
		}
		public HashTable_stringobject Dictionary { get { return dict; } }
		public ICollection<string> Keys => dict.Keys;
		public ICollection<object> Values => dict.Values;
		public int Count => dict.Count;
		public bool IsReadOnly => dict.IsReadOnly;
		public object this[string key] { get => dict[key]; set => dict[key] = value; }
		public void Add(string key, object value) { dict.Add(key, value); }
		public bool ContainsKey(string key) { return dict.ContainsKey(key); }
		public bool Remove(string key) { return dict.Remove(key); }
		public bool TryGetValue(string key, out object value) { return dict.TryGetValue(key, out value); }
		public void Add(KeyValuePair<string, object> item) { dict.Add(item); }
		public void Clear() { dict.Clear(); }
		public bool Contains(KeyValuePair<string, object> item) { return dict.Contains(item); }
		public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex) { dict.CopyTo(array, arrayIndex); }
		public bool Remove(KeyValuePair<string, object> item) { return dict.Remove(item); }
		public IEnumerator<KeyValuePair<string, object>> GetEnumerator() { return dict.GetEnumerator(); }
		IEnumerator IEnumerable.GetEnumerator() { return dict.GetEnumerator(); }
	}
}
