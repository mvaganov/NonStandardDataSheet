using NonStandard.Data;
using NonStandard.Ui;
using UnityEngine;
using TMPro;
using System.Collections.Generic;

namespace NonStandard.GameUi.DataSheet {
	public class ColumnHeader : MonoBehaviour {
		[SerializeField] private ColumnHeaderEditor editUiPrefab;
		//[ContextMenuItem("PopulateDropdown", "PopulateDropdown")] public GameObject editUi;
		public Udash.ColumnSetting columnSetting;
		// multiple different column headers can exist, each with its own ColumheaderEditor
		static Dictionary<ColumnHeaderEditor, ColumnHeaderEditor> instanceOfPrefab = 
			new Dictionary<ColumnHeaderEditor, ColumnHeaderEditor>();
		public ColumnHeaderEditor EditUi {
			get {
				if (!instanceOfPrefab.TryGetValue(editUiPrefab, out ColumnHeaderEditor found)) {
					instanceOfPrefab[editUiPrefab] = found = Instantiate(editUiPrefab.gameObject).GetComponent<ColumnHeaderEditor>();
					found.transform.SetParent(UDS().transform.parent);
					found.transform.localPosition = Vector3.zero;
				}
				return found;
			}
		}
		private void Start() {
			if (editUiPrefab == null) {
				editUiPrefab = transform.parent.GetComponentInChildren<ColumnHeaderEditor>();
			}
		}
		int Col() { return transform.GetSiblingIndex(); }
		TMP_Dropdown DD() { return GetComponent<TMP_Dropdown>(); }
		UnityDataSheet UDS() { return GetComponentInParent<UnityDataSheet>(); }
		public void ColumnNoSort() { SetSortMode((int)SortState.None); }
		public void ColumnSortAscend() { SetSortMode((int)SortState.Ascending); }
		public void ColumnSortDescend() { SetSortMode((int)SortState.Descening); }
		public void Close() {
			gameObject.SetActive(false);
		}
		public void ColumnEdit() {
			EditUi.gameObject.SetActive(true);
			//editUi.transform.SetParent(transform, false);
			//Debug.Log("path is " + editUi.transform.HierarchyPath());
			//editUi.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
			EditUi.SetColumnHeader(this, UDS(), Col());
			//Proc.Enqueue(() => {
			//	if (editUi != null) {
			//		editUi.transform.SetParent(originalParent, true);
			//	}
			//});
		}
		public void SetSortMode(int sortModeIndex) {
			if (sortModeIndex < 0 || sortModeIndex >= (int)SortState.Count) { return; }
			UnityDataSheet uds = UDS();
			int col = transform.GetSiblingIndex();
			uds.SetSortState(col, (SortState)sortModeIndex);
		}
		public void PopulateDropdown() {
			DropDownEvent dde = GetComponent<DropDownEvent>();
			if (dde == null) { dde = gameObject.AddComponent<DropDownEvent>(); }
			dde.options = new System.Collections.Generic.List<ModalConfirmation.Entry> {
				new ModalConfirmation.Entry("No Sort", this, nameof(ColumnNoSort)),
				new ModalConfirmation.Entry("Sort Ascending", this, nameof(ColumnSortAscend)),
				new ModalConfirmation.Entry("Sort Descending", this, nameof(ColumnSortDescend)),
				new ModalConfirmation.Entry("Edit Column", this, nameof(ColumnEdit), false),
				//new ModalConfirmation.Entry("Remove Column", this, nameof(ColumnRemove), false),
			};
			dde.PopulateDropdown();
		}
	}
}