using System.Collections.Generic;
using UnityEngine;

namespace NonStandard.GameUi.DataSheet {
	public class DataSheetStyleOptions : MonoBehaviour {
		public DataSheetRow dataSheetRow;
		public List<GameObject> notCellTypes;
		Dictionary<string, GameObject> cellTypes = new Dictionary<string, GameObject>();
		public void Init() {
			for (int i = 0; i < transform.childCount; ++i) {
				Transform t = transform.GetChild(i);
				if (t == null) continue;
				cellTypes[t.name] = t.gameObject;
			}
			if (notCellTypes == null) { notCellTypes = new List<GameObject>(); }
			notCellTypes.Add(dataSheetRow.gameObject);
			for (int i = 0; i < notCellTypes.Count; ++i) {
				GameObject c = notCellTypes[i];
				if (cellTypes.TryGetValue(c.name, out GameObject found) && found == c) { cellTypes.Remove(c.name); }
			}
		}
		public void Awake() { Init(); }
		public GameObject GetElement(string name) {
			if (cellTypes.Count == 0) { Init(); }
			cellTypes.TryGetValue(name, out GameObject found);
			//Show.Log(name + ": " + found);
			return found;
		}
	}
}