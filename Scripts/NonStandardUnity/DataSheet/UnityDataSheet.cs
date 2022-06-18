// code by michael vaganov, released to the public domain via the unlicense (https://unlicense.org/)
using NonStandard.Data;
using NonStandard.Data.Parse;
using NonStandard.Extension;
using NonStandard.Process;
using NonStandard.Ui;
using NonStandard.Utility.UnityEditor;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

/// <summary>
/// model/view controllers are tricky because cache invalidation is hard.
/// </summary>
namespace NonStandard.GameUi.DataSheet {
	public interface IHasUiElement {
		GameObject UiElement { get; set; }
	}
	/// <summary>
	/// the actual data structure that is used 
	/// </summary>
	public class UnityColumnData : ColumnData {
		/// <summary>
		/// what data goes into each cell of the column
		/// </summary>
		public Token valueScript;
		/// <summary>
		/// prefab used for the column data elements
		/// </summary>
		public Token columnUi;
		/// <summary>
		/// prefab used for the column header element
		/// </summary>
		public Token headerUi;
		/// <summary>
		/// a script to execute when this element is clicked. row object as scope
		/// </summary>
		public Token onClick;
		public object defaultValue; // TODO make this a Token, like valueScript -> fieldToken
		public Type typeOfValue;
		/// <summary>
		/// width of the column
		/// </summary>
		public float widthOfColumn;
		/// <summary>
		/// if true, will move itself to far right end, even if new elements are added
		/// </summary>
		public bool alwaysLast;
	}
	public class ClickableScriptedCell : MonoBehaviour {
		public Token script;
		public object scope;
		public string debugMetaData;
		public Action<string> onError;
		public void Set(object scope, Token script) { this.scope = scope; this.script = script; }
		/// <summary>
		/// how to execute an onClick action
		/// </summary>
		public void OnClick() {
			//Show.Log(debugMetaData);
			//Show.Log("onClick " + scope + "." + script.Stringify());
			TokenErrorLog err = new TokenErrorLog();
			if (script.meta != null) {
				object r = script.Resolve(err, scope);
			}
			if (err.HasError()) {
				string errString = err.GetErrorString();
				Show.Warning(errString);
				onError?.Invoke(errString);
				if (!string.IsNullOrEmpty(debugMetaData)) {
					err.AddError(debugMetaData);
				}
			}
		}
		public void OnClick(BaseEventData bed) { OnClick(); }
	}
	public class DataSheetUnityColumnData : DataSheetBase<UnityColumnData> { }
	public class UnityDataSheet : MonoBehaviour {
		const int columnTitleIndex = 0, uiTypeIndex = 1, valueIndex = 2, headerUiType = 3, columnWidth = 4, defaultValueIndex = 5;
		public RectTransform headerRectangle;
		public RectTransform contentRectangle;
		public DataSheetUnityColumnData data = new DataSheetUnityColumnData();
		public DataSheetStyleOptions uiPrototypes;
		protected RectTransform rt;
		internal TokenErrorLog errLog = new TokenErrorLog();
		public UiHoverPopup popup;
		[TextArea(1, 10)]
		public string columnSetup;
		public UnityEvent_List_object dataPopulator = new UnityEvent_List_object();
		public UnityEvent_RowsL onNotifyReorder = new UnityEvent_RowsL();
		Vector2 contentAreaSize;
		bool needsRefresh;
		bool debugNoisy;
		public bool alwaysRebuildUi = false;
		public int Count => data.rows.Count;

		[Serializable] public class UnityEvent_RowsL : UnityEvent<List<RowData>> { }

		public int GetRowIndex(GameObject rowObject) {
			for(int i = 0; i < contentRectangle.childCount; ++i) {
				if (contentRectangle.GetChild(i).gameObject == rowObject) {
					return i;
				}
			}
			return -1;
		}

		private void Awake() {
			rt = GetComponent<RectTransform>();
		}
		private void Init() {
			rt = GetComponent<RectTransform>();
			data = new DataSheetUnityColumnData();
			InitColumnSettings(columnSetup);
		}
		void InitColumnSettings(string columnSetup) {
			//Debug.Log(columnSetup);
			Tokenizer tokenizer = new Tokenizer();
			CodeConvert.TryParse(columnSetup, out UnityColumnData[] columns, null, tokenizer);
			if (tokenizer.HasError()) {
				ShowError(tokenizer.GetErrorString());
				Debug.LogError("error parsing column structure: " + tokenizer.GetErrorString());
				return;
			}
			int index = 0;
			//data.AddRange(list, tokenizer);
			for (int i = 0; i < columns.Length; ++i) {
				UnityColumnData c = columns[i];
				c.typeOfValue = c.defaultValue != null ? c.defaultValue.GetType() : null;
				DataSheetUnityColumnData.ColumnSetting columnSetting = new DataSheetUnityColumnData.ColumnSetting(data) {
					//fieldToken = c.valueScript,
					data = new UnityColumnData {
						label = c.label,
						columnUi = c.columnUi,
						headerUi = c.headerUi,
						widthOfColumn = -1,
						onClick = c.onClick,
						alwaysLast = c.alwaysLast
					},
					type = c.typeOfValue,
					defaultValue = c.defaultValue
				};
				if (columnSetting.type == null && c.defaultValue != null) {
					//Debug.Log("setting default type to "+ c.defaultValue.GetType());
					columnSetting.type = c.defaultValue.GetType();
				}
				columnSetting.SetFieldToken(c.valueScript, tokenizer);
				data.SetColumn(index, columnSetting);
				if (c.widthOfColumn > 0) {
					data.columnSettings[index].data.widthOfColumn = c.widthOfColumn;
				}
				//Debug.Log("column " + index + " " + c.label);
				++index;
			}
			RefreshHeaders();
		}
		public void ShowError(string error) => ShowError(error, null);
		public void ShowError(string error, GameObject errorObject) {
			popup.Set("err", errorObject, error);
		}
		public void QueueRefresh() {
			needsRefresh = true;
		}
		public void RefreshData() {
			List<object> objects;
			if (alwaysRebuildUi) {
				data.Clear();
				objects = GetObjects(); 
				Load(objects);
				Debug.Log("hard refresh");
				FullRefresh();
				return;
			}
			//string getName(object obj) { return (obj as UnityEngine.Object).name; }
			Dictionary<object, int> objectsToFilterOut = GetManifestOfObjectsInUi();
			objects = GetCurrentObjectsRespectingUiOrder(objectsToFilterOut);
			//Debug.Log("REFRESH  " + objects.JoinToString(", ", getName));
			Dictionary<object, int> toAdd = ProcessChangesNeededToUi(objects, objectsToFilterOut);
			//Debug.Log("REFRESH:\nadd " + toAdd.Keys.JoinToString(", ", getName) + "\nremove: " + objectsToFilterOut.JoinToString(", ", getName));
			RemoveUiFor(objectsToFilterOut);
			AddUiForObjectsInOrder(toAdd);

			//Debug.Log("REFRESHd " + objects.JoinToString(", ", getName));
			//data.Clear();
			//Load(objects);
			FullRefresh();
		}

		public List<object> GetCurrentObjectsRespectingUiOrder(Dictionary<object, int> objectOrdering) {
			List<object> objects = GetObjects();
			//Debug.Log("pre sort:" + objects.JoinToString());
			objects.Sort((a, b) => {
				if (!objectOrdering.TryGetValue(a, out int orderA)) { orderA = -1; }
				if (!objectOrdering.TryGetValue(b, out int orderB)) { orderB = -1; }
				if (orderA >= 0 && orderB >= 0) { return orderA.CompareTo(orderB); }
				if (orderA >= 0) { return -1; }
				if (orderB >= 0) { return 1; }
				return 0;
			});
			//Debug.Log("postsort:" + objects.JoinToString());
			return objects;
		}

		public List<object> GetObjects() {
			List<object> objects = new List<object>();
			dataPopulator.Invoke(objects);
			for (int i = objects.Count - 1; i >= 0; --i) {
				if (objects[i] == null) {
					Show.Warning("{" + EventBind.DebugPrint(dataPopulator) + "}[" + i + "] is null. removing it");
					objects.RemoveAt(i);
				}
			}
			return objects;
		}

		public Dictionary<object,int> GetManifestOfObjectsInUi() {
			Dictionary<object,int> manifest = new Dictionary<object, int>();
			for (int i = 0; i < data.rows.Count; ++i) {
				object o = data.rows[i].obj;
				if (o != null) {
					if (manifest.ContainsKey(o)) { throw new Exception("old data contains duplicate " + o + " at index " + i); }
					manifest[o] = i;
				}
			}
			return manifest;
		}

		private Dictionary<object,int> ProcessChangesNeededToUi(List<object> expectedListing, Dictionary<object,int> filterExistingOut) {
			Dictionary<object, int> toAdd = new Dictionary<object, int>();
			for (int i = 0; i < expectedListing.Count; ++i) {
				object o = expectedListing[i];
				bool objectIsMissingInUi = !filterExistingOut.ContainsKey(o);
				bool objectUiHasChanged = true; // TODO compare new values to currently displayed values
				if (objectIsMissingInUi || objectUiHasChanged) {
					if (toAdd.TryGetValue(o, out int index)) {
						throw new Exception("new data contains duplicate " + o + " at index " + index + " and index " + i);
					}
					toAdd[o] = i;
				} else {
					filterExistingOut.Remove(o);
				}
			}
			return toAdd;
		}

		public void RemoveUiFor(Dictionary<object,int> objectsToFilterOut) {
			for (int i = data.rows.Count - 1; i >= 0; --i) {
				if (objectsToFilterOut.ContainsKey(data.rows[i].obj)) {
					data.rows.RemoveAt(i);
				}
			}
		}

		public void AddUiForObjectsInOrder(Dictionary<object,int> toAdd) {
			List<KeyValuePair<object, int>> values = toAdd.GetPairs();
			values.Sort((a, b) => a.Value.CompareTo(b.Value));
			for (int i = 0; i < values.Count; ++i) {
				int index = values[i].Value;
				if (index < data.rows.Count) {
					data.InsertRow(index, values[i].Key);
				} else {
					data.AddRow(values[i].Key);
				}
			}
		}

		public void RefreshHeaders() {
			if (headerRectangle == null) return;
			Vector2 cursor = Vector2.zero;
			// put old headers aside. they may be reused.
			List<GameObject> unusedHeaders = new List<GameObject>();
			for (int i = 0; i < headerRectangle.childCount; ++i) {
				GameObject header = headerRectangle.GetChild(i).gameObject;
				if (header != null) { unusedHeaders.Add(header); }
			}
			while(headerRectangle.childCount > 0) {
				Transform t = headerRectangle.GetChild(headerRectangle.childCount-1);
				t.SetParent(null, false);
			}
			errLog.ClearErrors();
			for (int i = 0; i < data.columnSettings.Count; ++i) {
				DataSheetUnityColumnData.ColumnSetting colS = data.columnSettings[i];
				GameObject header = null;
				string headerObjName = colS.data.headerUi.ResolveString(errLog, this);
				// check if the header we need is in the old header list
				object headerTextResult = colS.data.label.Resolve(errLog, data);
				if (errLog.HasError()) { ShowError(errLog.GetErrorString()); return; }
				string headerTextString = headerTextResult?.ToString() ?? null;
				for (int h = 0; h < unusedHeaders.Count; ++h) {
					GameObject hdr = unusedHeaders[h];
					if (hdr.name.StartsWith(headerObjName) && UiText.GetText(hdr) == headerTextString) {
						header = hdr;
						unusedHeaders.RemoveAt(h);
						break;
					}
				}
				if (header == null) {
					GameObject headerObjectPrefab = uiPrototypes.GetElement(headerObjName);
					header = Instantiate(headerObjectPrefab);
					header.name = header.name.SubstringBeforeFirst("(", headerObjName.Length) + "(" + colS.data.label + ")";
					UiText.SetText(header, headerTextString);
				}
				ColumnHeader ch = header.GetComponent<ColumnHeader>();
				if (ch != null) { ch.columnSetting = colS; }
				header.SetActive(true);
				header.transform.SetParent(headerRectangle, false);
				header.transform.SetSiblingIndex(i);
				RectTransform rect = header.GetComponent<RectTransform>();
				rect.anchoredPosition = cursor;
				float w = rect.sizeDelta.x;
				if (colS.data.widthOfColumn > 0) {
					w = colS.data.widthOfColumn;
					rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w);
				} else {
					colS.data.widthOfColumn = w; // if the width isn't set, use the default width of the column header
				}
				cursor.x += w * rt.localScale.x;
			}
			contentAreaSize.x = cursor.x;
			for (int i = 0; i < unusedHeaders.Count; ++i) {
				GameObject header = unusedHeaders[i];
				Destroy(header);
			}
			unusedHeaders.Clear();
			contentRectangle.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, cursor.x);
			popup.Hide();
		}
		public void RefreshContentSize() {
			contentRectangle.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, contentAreaSize.x);
			contentRectangle.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentAreaSize.y);
		}

		internal int GetMaximumUserColumn() {
			int max = data.columnSettings.Count;
			int countLastOnes = 0;
			for (int i = data.columnSettings.Count - 1; i >= 0; --i) {
				if (data.columnSettings[i].data.alwaysLast) { ++countLastOnes; }
			}
			max -= countLastOnes;
			return max;
		}

		private void Start() {
			if (uiPrototypes == null) {
				uiPrototypes = Global.GetComponent<DataSheetStyleOptions>();
				if(uiPrototypes == null) {
					throw new Exception("cannot create a data sheet without a style guide");
				}
			}
			Init();
			QueueRefresh();
			//Proc.Enqueue(() => { RefreshData(); });
			//string test = "{a:1,b:[a,[1,2],{a:a,b:[b]}],c:{a:1,b:2}}";
			//CodeConvert.TryParse(test, out object obj);
			//Show.Log(obj.Stringify(pretty:true));
		}
		private void Update() {
			if (needsRefresh) {
				needsRefresh = false;
				RefreshData();
			}
		}

		public void Load(List<object> source) {
			//list = source;
			data.InitData(source, errLog);
			if (errLog.HasError()) { ShowError(errLog.GetErrorString()); return; }
			RefreshUi();
			popup.Hide();
		}

		DataSheetRow CreateRow(int rowIndex, RowData rowData, float yPosition = float.NaN) {
			GameObject rowUi = Instantiate(uiPrototypes.dataSheetRow.gameObject);
			DataSheetRow rObj = rowUi.GetComponent<DataSheetRow>();
			if (rObj == null) { throw new Exception("RowUI prefab must have " + nameof(DataSheetRow) + " component"); }
			rObj.rowData = rowData;
			if (rObj.rowData == null) { throw new Exception("something bad. where is the object that this row is for?"); }
			rowUi.SetActive(true);
			UpdateRowData(rObj, rowIndex, rowData, yPosition);
			if (rowData.obj is IHasUiElement hasUi) {
				hasUi.UiElement = rObj.gameObject;
			}
			return rObj;
		}
		public GameObject UpdateRowData(DataSheetRow rObj, int rowIndex, RowData rowData, float yPosition = float.NaN, bool alsoCalculateColumns = true) {
			object[] columns = rowData.columns;
			Vector2 rowCursor = Vector2.zero;
			RectTransform rect;
			// remove all columns from the row (probably temporarily)
			List<GameObject> unusedColumns = new List<GameObject>();
			for(int i = 0; i < rObj.transform.childCount; ++i) {
				GameObject fieldUi = rObj.transform.GetChild(i).gameObject;
				if (fieldUi == null) { throw new Exception("a null child in the row? wat"); }
				unusedColumns.Add(fieldUi);
			}
			while(rObj.transform.childCount > 0) {
				rObj.transform.GetChild(rObj.transform.childCount-1).SetParent(null, false);
			}
			TokenErrorLog errLog = new TokenErrorLog();
			for (int c = 0; c < data.columnSettings.Count; ++c) {
				DataSheetUnityColumnData.ColumnSetting colS = data.columnSettings[c];
				GameObject fieldUi = null;
				string columnUiName = colS.data.columnUi.ResolveString(errLog, rowData.obj);
				if (columnUiName == null) {
					string errorMessage = "could not resolve column UI name from " + colS.data.columnUi+"\n"+errLog.GetErrorString();
					Show.Log(errorMessage);
					columnUiName = colS.data.columnUi.ResolveString(errLog, rowData.obj);
					throw new Exception(errorMessage);
				}
				// check if there's a version of it from earlier
				for (int i = 0; i < unusedColumns.Count; ++i) {
					if (unusedColumns[i].name.StartsWith(columnUiName)) {
						fieldUi = unusedColumns[i];
						unusedColumns.RemoveAt(i);
						break;
					}
				}
				// otherwise create it
				if (fieldUi == null) {
					GameObject prefab = uiPrototypes.GetElement(columnUiName);
					if (prefab == null) {
						columnUiName = colS.data.columnUi.ResolveString(errLog, rowData.obj);
						throw new Exception("no such prefab \""+columnUiName+"\" in data sheet initialization script. valid values: ["+
							uiPrototypes.transform.JoinToString()+"]\n---\n"+ colS.data.columnUi+"\n---\n"+columnSetup);
					}
					fieldUi = Instantiate(prefab);
				}

				if (colS.data.onClick.IsSyntax) {
					ClickableScriptedCell clickable = fieldUi.GetComponent<ClickableScriptedCell>();
					UiClick.ClearOnClick(fieldUi);
					if (fieldUi != null) { Destroy(clickable); }
					clickable = fieldUi.AddComponent<ClickableScriptedCell>();
					clickable.Set(rowData.obj, colS.data.onClick);
					clickable.onError += ShowError;
					clickable.debugMetaData = colS.data.onClick.StringifySmall();
					if (!UiClick.AddOnButtonClickIfNotAlready(fieldUi, clickable, clickable.OnClick)) {
						UiClick.AddOnPanelClickIfNotAlready(fieldUi, clickable, clickable.OnClick);
					}
				}

				fieldUi.SetActive(true);
				fieldUi.transform.SetParent(rObj.transform, false);
				fieldUi.transform.SetSiblingIndex(c);
				//columns[c] = data.RefreshValue(rowIndex, c, errLog);
				//if (errLog.HasError()) { throw new Exception(errLog.GetErrorString()); }
				object value = columns[c];
				//Debug.Log("  "+value+" ::::: "+data.columnSettings[c].editPath?.JoinToString() ?? "NOPATH?"); // TODO why is this a different (wrong) value if it is not the first element in the table?
				if (value != null) {
					UiText.SetText(fieldUi, value.ToString());
				} else {
					UiText.SetText(fieldUi, "");
				}
				rect = fieldUi.GetComponent<RectTransform>();
				rect.anchoredPosition = rowCursor;
				float w = rect.sizeDelta.x;
				if (colS.data.widthOfColumn > 0) {
					w = colS.data.widthOfColumn;
					rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w);
				}
				rowCursor.x += w * rt.localScale.x;
			}
			for(int i = 0; i < unusedColumns.Count; ++i) {
				//Debug.Log("destroying "+unusedColumns[i].name);
				Destroy(unusedColumns[i].gameObject);
			}
			unusedColumns.Clear();
			rect = rObj.GetComponent<RectTransform>();
			rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, rowCursor.x);
			rect.transform.SetParent(contentRectangle, false);
			if (!float.IsNaN(yPosition)) {
				//rect.anchoredPosition = new Vector2(0, -yPosition);
				//rect.localPosition = new Vector2(0, -yPosition);
				rObj.LocalPosition = new Vector2(0, -yPosition);
			}
			return rObj.gameObject;
		}

		public void RefreshColumnText(int column, ITokenErrLog errLog) {
			for(int row = 0; row < contentRectangle.childCount; ++row) {
				GameObject fieldUi = contentRectangle.GetChild(row).GetChild(column).gameObject;
				object value = data.RefreshValue(row, column, errLog);
				if (errLog.HasError()) return;
				if (value != null) {
					UiText.SetText(fieldUi, value.ToString());
					//sb.Append(value.ToString() + ", ");
				} else {
					UiText.SetText(fieldUi, "");
				}
			}
		}
		public void RefreshUi() {
			RefreshHeaders();
			RefreshRowAndColumnUi();
		}

		public void FullRefresh() {
			errLog.ClearErrors();
			data.RefreshAll(errLog);
			if(errLog.HasError()) { return; }
			RefreshUi();
			Proc.Enqueue(RefreshContentSize); // wait one frame, till after child UI components recalculate
		}
		public void ResizeColumnWidth(int column, float oldWidth, float newWidth) {
			//Show.Log("TODO resize width of column "+column+" from "+oldWidth+" to "+newWidth);
			data.columnSettings[column].data.widthOfColumn = newWidth;
			RefreshUi();
		}
		public void MoveColumn(int oldIndex, int newIndex) {
			// need to re-arrange headers in data
			data.MoveColumn(oldIndex, newIndex);
			// change the index of the column in the header (UI)
			headerRectangle.GetChild(oldIndex).SetSiblingIndex(newIndex);
			RefreshUi();
		}

		public void MoveRow(int oldIndex, int newIndex) {
			data.MoveRow(oldIndex, newIndex);
			contentRectangle.GetChild(oldIndex).SetSiblingIndex(newIndex);
			RefreshRowUi();
			onNotifyReorder?.Invoke(data.rows);
		}
		public void RemoveRow(int index) {
			Transform child = contentRectangle.transform.GetChild(index);
			child.SetParent(null);
			Destroy(child.gameObject);
			data.RemoveRow(index);
			RefreshRowUi();
		}
		public void RemoveColumn(int index) {
			Transform column = headerRectangle.transform.GetChild(index);
			column.SetParent(null);
			Destroy(column.gameObject);
			data.RemoveColumn(index);
			RefreshUi();
		}
		public static void NotifyReorder<T>(List<RowData> reordered, List<T> source) where T : class {
			Dictionary<T, int> dataIndex = new Dictionary<T, int>();
			for (int i = 0; i < reordered.Count; i++) {
				T element = reordered[i].obj as T;
				if (element == null) {
					Debug.LogError("expected NotifyReorder to give RowData of " + nameof(T) + " objects");
					return;
				}
				dataIndex[element] = i;
			}
			source.Sort((a, b) => {
				if (dataIndex.TryGetValue(a, out int indexA) && dataIndex.TryGetValue(b, out int indexB)) {
					return indexA.CompareTo(indexB);
				}
				return 0;
			});
		}
		public Dictionary<object, DataSheetRow> RefreshRowUi_HardReset
															() {
			for (int i = 0; i < contentRectangle.childCount; ++i) {
				DataSheetRow rObj = contentRectangle.GetChild(i).GetComponent<DataSheetRow>();
				if (rObj) {
					rObj.transform.SetParent(null);
					Destroy(rObj.gameObject);
				}
			}
			Vector2 cursor = Vector2.zero;
			Dictionary<object, DataSheetRow> usedMapping = new Dictionary<object, DataSheetRow>(); 
			for (int i = 0; i < data.rows.Count; ++i) {
				RowData rd = data.rows[i];
				DataSheetRow rObj = CreateRow(i, data.rows[i], -cursor.y);
				//Debug.Log("creating row for "+rd.obj);
				rObj.transform.SetParent(contentRectangle);
				usedMapping[rObj.obj] = rObj;
			//	rObj.transform.SetSiblingIndex(i);
				RectTransform rect = rObj.GetComponent<RectTransform>();
				//rect.anchoredPosition = cursor;
				//rect.localPosition = cursor;
				rObj.LocalPosition = cursor;
				cursor.y -= rect.rect.height;
			}
			contentAreaSize.y = -cursor.y;
			contentRectangle.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, -cursor.y);
			return usedMapping;
		}

		/// <summary>
		/// uses a dictionary to quickly calculate UI elements for rows, and position them in the view
		/// </summary>
		/// TODO refactor this method... something fishy is going on in here maybe.
		public Dictionary<object, DataSheetRow> RefreshRowUi() {
			Dictionary<object, DataSheetRow> oldMap = MapOfCurrentUi();
			// check to see if any of the UI rows are not being used by the datasheet by identifying which ones are used for sure
			Dictionary<object, DataSheetRow> usedMapping = MapOfUiNeededForCurrentData(oldMap);
			List<DataSheetRow> unused = ListCurrentUiElementsThatAreNotNeeded(usedMapping);
			Vector2 cursor = Vector2.zero;
			// go through all of the row elements and put the row UI elements in the correct spot
			for(int i = 0; i < data.rows.Count; ++i) {
				DataSheetRow uiElement = PutDataIntoUiElement(data.rows[i], i, cursor, usedMapping, unused);
				RectTransform rectOfUiElement = uiElement.GetComponent<RectTransform>();
				cursor.y -= rectOfUiElement.rect.height;
			}
			if (contentRectangle.childCount > data.rows.Count || unused.Count > 0) {
				ClearUnusedUiElements(oldMap, unused);
			}
			contentAreaSize.y = -cursor.y;
			contentRectangle.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, -cursor.y);
			return usedMapping;
		}

		private Dictionary<object, DataSheetRow> MapOfCurrentUi() {
			Dictionary<object, DataSheetRow> currentUi = new Dictionary<object, DataSheetRow>();
			for (int i = 0; i < contentRectangle.childCount; ++i) {
				DataSheetRow rObj = contentRectangle.GetChild(i).GetComponent<DataSheetRow>();
				if (rObj == null) { continue; }
				if (rObj.obj == null) {
					throw new Exception("found a row (" + rObj.transform.HierarchyPath() + ") without source object at index " + i);
				}
				if (currentUi.TryGetValue(rObj.obj, out DataSheetRow uiElement)) {
					throw new Exception("multiple row elements for row " + i + ": " + rObj.obj);
				}
				currentUi[rObj.obj] = rObj;
			}
			return currentUi;
		}
		private Dictionary<object, DataSheetRow> MapOfUiNeededForCurrentData(Dictionary<object, DataSheetRow> currentUiData) {
			Dictionary<object, DataSheetRow> usedMapping = new Dictionary<object, DataSheetRow>();
			//List<object> missing = new List<object>();
			for (int i = 0; i < data.rows.Count; ++i) {
				RowData rd = data.rows[i];
				if (currentUiData.TryGetValue(rd.obj, out DataSheetRow foundRow)) {
					usedMapping[rd.obj] = foundRow;
				}
				//else { missing.Add(rd.obj); }
			}
			return usedMapping;
		}
		private List<DataSheetRow> ListCurrentUiElementsThatAreNotNeeded(
		Dictionary<object, DataSheetRow> currentUiData) {
			List<DataSheetRow> unused = new List<DataSheetRow>();
			for (int i = 0; i < contentRectangle.childCount; ++i) {
				DataSheetRow currentUi = contentRectangle.GetChild(i).GetComponent<DataSheetRow>();
				if (!currentUiData.TryGetValue(currentUi.obj, out DataSheetRow foundRow)) {
					unused.Add(currentUi);
				}
			}
			return unused;
		}

		private DataSheetRow PutDataIntoUiElement(RowData rd, int i, Vector2 cursor, 
		Dictionary<object, DataSheetRow> currentUiData, List<DataSheetRow> unused) {
			// if this row data is missing a UI element
			if (currentUiData.TryGetValue(rd.obj, out DataSheetRow rObj)) {
				// Debug.Log("reusing UI row for "+rd.obj);
				data.RefreshRowData(rd, data.columnSettings);
				UpdateRowData(rObj, i, rd, -cursor.y);
			} else {
				rObj = GiveDataModelSomeUi(unused, i, rd, -cursor.y, currentUiData);
			}
			rObj.transform.SetSiblingIndex(i);
			rObj.LocalPosition = cursor;
			return rObj;
		}

		private DataSheetRow GiveDataModelSomeUi(List<DataSheetRow> unused, int i, RowData rd, float yPosition,
		Dictionary<object, DataSheetRow> usedMapping) {
			DataSheetRow rObj;
			// use one of the unused elements if there is one
			if (unused.Count > 0) {
				rObj = unused[unused.Count - 1];
				unused.RemoveAt(unused.Count - 1);
				UpdateRowData(rObj, i, rd, yPosition);
			} else {
				// create he UI element, and put it into the content rectangle
				rObj = CreateRow(i, data.rows[i], yPosition);
			}
			rObj.transform.SetParent(contentRectangle);
			usedMapping[rObj.obj] = rObj;
			return rObj;
		}
		private void ClearUnusedUiElements(Dictionary<object, DataSheetRow> oldMap, List<DataSheetRow> unused) {
			// remove unused old UI elements in reverse order (should be slightly faster than in order)
			for (int i = contentRectangle.childCount - 1; i >= data.rows.Count; --i) {
				DataSheetRow rObj = contentRectangle.GetChild(i).GetComponent<DataSheetRow>();
				oldMap.Remove(rObj.obj);
				if (!unused.Contains(rObj)) { unused.Add(rObj); }
			}
			if (debugNoisy) {
				Show.Log("deleting extra elements: " + unused.JoinToString(", ", go => {
					DataSheetRow ro = go.GetComponent<DataSheetRow>();
					if (ro == null) return "<null>";
					if (ro.obj == null) return "<null obj>";
					return ro.obj.ToString();
				}));
			}
			for (int i = unused.Count - 1; i >= 0; --i) {
				unused[i].transform.SetParent(null);
				Destroy(unused[i].gameObject);
			}
			unused.Clear();
		}

		public void SetSortState(int column, SortState sortState) {
			RefreshRowAndColumnUi();
			data.SetSortState(column, sortState);
			RefreshRowUi();
		}
		public object GetItem(int index) { return data.rows[index].obj; }
		public object SetItem(int index, object dataModel) { return data.rows[index].obj = dataModel; }
		public int IndexOf(object dataModel) { return data.IndexOf(dataModel); }
		public int IndexOf(Func<object, bool> predicate) { return data.IndexOf(predicate); }
		public void RefreshRowAndColumnUi() {
			RefreshRowUi();
			if (data.rows.Count != contentRectangle.childCount) {
				Debug.LogWarning($"contentRectangle ({contentRectangle.childCount}) does not reflect data.rows ({data.rows.Count})");
			}
			// for some reason, everything was recalculated at one point?
			//float y = 0;
			//for (int i = 0; i < data.rows.Count; ++i) {
			//	DataSheetRow rObj = contentRectangle.GetChild(i).GetComponent<DataSheetRow>();
			//	UpdateRowData(rObj, data.rows[i], y, false); // no need to update row data again, it was done with RefreshRowUi()
			//	y += rObj.GetComponent<RectTransform>().rect.height;
			//}
		}
		public void Sort() {
			RefreshRowAndColumnUi();
			if (data.Sort()) {
				RefreshRowUi();
			}
		}
		public DataSheetUnityColumnData.ColumnSetting GetColumn(int index) { return data.GetColumn(index); }

		public DataSheetUnityColumnData.ColumnSetting AddColumn() {
			DataSheetUnityColumnData.ColumnSetting column = new DataSheetUnityColumnData.ColumnSetting(data) {
				fieldToken = new Token(""),
				data = new UnityColumnData {
					label = new Token("new data"),
					columnUi = new Token("input"),
					headerUi = new Token("collabel"),
					widthOfColumn = -1,
				},
				type = typeof(string),
				defaultValue = ""
			};
			data.AddColumn(column);
			MakeSureColumnsMarkedLastAreLast();
			RefreshUi();
			return column;
		}

		void MakeSureColumnsMarkedLastAreLast() {
			List<DataSheetUnityColumnData.ColumnSetting> moveToEnd = new List<DataSheetBase<UnityColumnData>.ColumnSetting>();
			for (int i = 0; i < data.columnSettings.Count; ++i) {
				if (data.columnSettings[i].data.alwaysLast) {
					moveToEnd.Add(data.columnSettings[i]);
					data.columnSettings.RemoveAt(i);
					--i;
				}
			}
			if (moveToEnd.Count > 0) {
				for (int i = 0; i < moveToEnd.Count; ++i) {
					data.columnSettings.Add(moveToEnd[i]);
				}
				moveToEnd.Clear();
			}

		}
	}
}
