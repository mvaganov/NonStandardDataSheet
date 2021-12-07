using NonStandard.Extension;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace NonStandard.Data {
	public partial class ScriptedDictionaryManager : MonoBehaviour {

		public List<ScriptedDictionary> dictionaries = new List<ScriptedDictionary>();
		private ScriptedDictionary mainDictionary;
		public ScriptedDictionary Main { get => mainDictionary; set => mainDictionary = value; }
		public void Register(ScriptedDictionary keeper) { dictionaries.Add(keeper); if (mainDictionary == null) mainDictionary = keeper; }
		public void Increment(string name) { mainDictionary.AddTo(name, 1); }
		public void Decrement(string name) { mainDictionary.AddTo(name, -1); }
		public ScriptedDictionary Find(Func<ScriptedDictionary, bool> predicate) { return dictionaries.Find(predicate); }
		public void SetMainDicionary(ScriptedDictionary scriptedDictionary) { mainDictionary = scriptedDictionary; }
	}
}
