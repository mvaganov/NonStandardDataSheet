using UnityEngine;

namespace NonStandard.GameUi.DataSheet {
	public class NewColumnButton : MonoBehaviour {
		public void AddColumn() {
			UnityDataSheet uds = GetComponentInParent<UnityDataSheet>();
			uds.AddColumn();
		}
	}
}