// code by michael vaganov, released to the public domain via the unlicense (https://unlicense.org/)
using NonStandard.Data.Parse;
using NonStandard.Extension;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace NonStandard.Data {
	public class ColumnData {
		/// <summary>
		/// label at the top of the column
		/// </summary>
		public Token label;
		/// <summary>
		/// hover-over description for column label
		/// </summary>
		public Token description;
	}
	public class RowData {
		public object obj;
		public object[] columns;
		public RowData(object model, object[] columns) { this.obj = model; this.columns = columns; }
	}
	public class DataSheet : DataSheetBase<ColumnData> {
		public DataSheet() : base() { }
	}
	public enum SortState { None, Ascending, Descening, Count }
	public class DataSheetBase<MetaData> where MetaData : new() {
		/// <summary>
		/// the actual data
		/// </summary>
		public List<RowData> rows = new List<RowData>();
		/// <summary>
		/// data about the columns
		/// </summary>
		public List<ColumnSetting> columnSettings = new List<ColumnSetting>();
		/// <summary>
		/// which column is sorted first?
		/// </summary>
		protected List<int> columnSortOrder = new List<int>();

		public class ColumnSetting {
			/// <summary>
			/// data sheet that this column belongs to
			/// </summary>
			internal DataSheetBase<MetaData> dataSheet;
			/// <summary>
			/// which field is displayed in this column? can include if statement logic.
			/// </summary>
			private Token _fieldToken;
			/// <summary>
			/// what field is being read (or modified) in this column
			/// </summary>
			public Token fieldToken { get => _fieldToken; set { SetFieldToken(value, null); } }
			/// <summary>
			/// the data path as it is accessed from the object that each row has data for
			/// </summary>
			public List<object> editPath;
			/// <summary>
			/// calculated when <see cref="fieldToken"/> is set to determine if data can be written back to the row object
			/// </summary>
			public bool canEdit;
			/// <summary>
			/// if the <see cref="_fieldToken"/> is an if-statement, then the field needs to be re-evaluated for each row.
			/// </summary>
			public bool mustReEvauluateFieldBecauseOfConditionalLogic;
			/// <summary>
			/// if the <see cref="_fieldToken"/> has an untyped <see cref="object"/> in the member path, then the field path needs to be re-evaluated for each row.
			/// </summary>
			public bool mustReEvauluateFieldPathBecauseOfConditionalLogic;
			/// <summary>
			/// the edit path is loaded if no edit path exists when values change
			/// </summary>
			public bool needsToLoadEditPath;
			/// <summary>
			/// what type the values in this column are expected to be
			/// </summary>
			public Type type;
			/// <summary>
			/// additional meta-data for each colum. eg: UI used to display? word-wrap? text alignment/direction? colorization rules? ...
			/// </summary>
			public MetaData data = new MetaData();
			/// <summary>
			/// expectations for sorting
			/// </summary>
			public SortState sortState = SortState.None;
			/// <summary>
			/// specific algorithms used to sort
			/// </summary>
			public Comparison<object> sort;
			/// <summary>
			/// what to resolve in this column if the path is missing or erroneous
			/// </summary>
			public object defaultValue = null; // TODO make this a Token that supports if-statements, like _fieldToken

			/// <summary>
			/// set the script that determines what the values in this column are
			/// </summary>
			/// <param name="value"></param>
			/// <param name="errLog"></param>
			/// <returns>a sample value (the first value possible)</returns>
			public object SetFieldToken(Token value, ITokenErrLog errLog) {
				_fieldToken = value;
				RefreshEditPath(true, null, errLog);
				if (errLog != null && errLog.HasError()) { return null; } // TODO print errors from the RefreshEditPath function, no?
				if (editPath != null && dataSheet.rows.Count > 0) {
					return RefreshStrictlyTypedVariablePathBasedOnDataFromSpreadsheet(errLog);
				}
				//else { Show.Log(_fieldToken.GetAsSmallText() + " is read only"); }
				return null;
			}
			object RefreshStrictlyTypedVariablePathBasedOnDataFromSpreadsheet(ITokenErrLog errLog) {
				object scope = dataSheet.GetItem(0); // TODO if GetItem(0) failed, try the other items?
				CompileEditPath(scope, errLog);
				if (errLog != null && errLog.HasError()) { return null; }
				ReflectionParseExtension.TryGetValueCompiledPath(scope, editPath, out object result);
				//Show.Log(_fieldToken.GetAsSmallText() + " is : " + result + (result != null ? (" (" + result.GetType() + ")") : "(null)"));
				return result;
			}
			object CompileEditPath(object scope, ITokenErrLog errLog = null) {
				if (editPath == null) {
					errLog.AddError(-1, fieldToken.GetAsSmallText() + " is not editable");
					return null;
				}
				//Show.Log("need to compile " + editPath.JoinToString());
				List<object> compiledPath = new List<object>();
				errLog?.ClearErrors();
				object result = ReflectionParseExtension.GetValueFromRawPath(scope, editPath, defaultValue, compiledPath, errLog);
				if (errLog != null && errLog.HasError()) {
					editPath = null;
					return null;
				}
				editPath = compiledPath;
				// check if the path has a member with members that can't be reasonably predicted
				for (int i = 0; i < editPath.Count; ++i) {
					if (editPath[i] is FieldInfo fi && (fi.FieldType == typeof(object) || fi.FieldType.IsAbstract)) {
						mustReEvauluateFieldPathBecauseOfConditionalLogic = true;
					}
				}
				//ReflectionParseExtension.TryGetValueCompiledPath(scope, editPath, out result);
				//Show.Log("compiled " + editPath.JoinToString(",",o=>o?.GetType()?.ToString() ?? "???")+" : "+result);
				needsToLoadEditPath = false;
				if (result == null && errLog != null) {
					errLog.AddError(0, "could not parse path: "+editPath.JoinToString());
				}
				return result;
			}

			public object GetValue(ITokenErrLog errLog, object scope) {
				object result;
				if (mustReEvauluateFieldPathBecauseOfConditionalLogic) {
					RefreshEditPath(false, scope, errLog);
					result = CompileEditPath(scope, errLog);
					if (errLog != null && errLog.HasError()) { return null; }
					return FilterType(result);
				} else if (mustReEvauluateFieldBecauseOfConditionalLogic) {
					RefreshEditPath(false, scope, errLog);
					if (errLog != null && errLog.HasError()) { return null; }
				}
				if (needsToLoadEditPath) {
					result = CompileEditPath(scope);
				} else if (editPath != null) {
					if(!ReflectionParseExtension.TryGetValueCompiledPath(scope, editPath, out result)) {
						result = defaultValue;
					}
				} else {
					bool errorNeedsToBeNoisy = false;
					if (errLog == null) { errLog = new TokenErrorLog(); errorNeedsToBeNoisy = true; }
					result = fieldToken.Resolve(errLog, scope);
					if (errLog.HasError()) {
						result = defaultValue;
						if (errorNeedsToBeNoisy) {
							throw new Exception("token \'"+fieldToken.GetAsSmallText()+"\' resolve error: "+errLog.GetErrorString());
						}
					}
				}
				return FilterType(result);
			}

			public object FilterType(object value) {
				if (type != null) { CodeConvert.Convert(ref value, type); }
				return value;
			}

			/// <summary>
			/// the <see cref="ColumnSetting"/> implies a data element for a list of objects
			/// </summary>
			/// <param name="scope">the specific object to change a value in</param>
			/// <param name="value">the value to assign</param>
			/// <returns></returns>
			public bool SetValue(object scope, object value, ITokenErrLog errLog = null) {
				//Show.Log("attempting to set " + _fieldToken.GetAsSmallText() + " to " + value);
				if (mustReEvauluateFieldBecauseOfConditionalLogic) {
					RefreshEditPath(false, scope, errLog);
					if (errLog.HasError()) { return false; }
				}
				if (!canEdit) return false;
				if (needsToLoadEditPath) {
					CompileEditPath(scope);
				}
				value = FilterType(value);
				if (editPath != null) {
					ReflectionParseExtension.TrySetValueCompiledPath(scope, editPath, value, errLog);
				} else {
					errLog.AddError(-1, "Cannot set value for " + _fieldToken.Stringify());
					return false;
				}

				// TODO if this variable path exists in another column, refresh that column as well.
				// TODO refresh columns that depend on data from this column.

				//ReflectionParseExtension.TryGetValueCompiledPath(scope, editPath, out object result);
				//Show.Log("set " + scope + "." + _fieldToken.GetAsSmallText() + " to " + result);
				return true;
			}
			public static bool ResolveToNonIfStatement(object thing) {
				if (thing is NonStandard.Data.Parse.SyntaxTree syntax && syntax.rules == CodeRules.IfStatement) { return false; } // IfStatement syntax is not "resolved enough"
				return true; // get the first thing that isn't an if-statement
			}
			/// <summary>
			/// 
			/// </summary>
			/// <param name="fieldToProcess">field with the conditional statement filter</param>
			/// <param name="errLog">where to put errors encountered during parsing</param>
			/// <param name="scope">where to get variables that need to be resolved in the token</param>
			/// <param name="checkOnly">if true, will not fully resolve the token, only identify that it needs to be resolved later</param>
			/// <returns></returns>
			public static bool ResolveConditionalFilter(ref Token fieldToProcess, ITokenErrLog errLog, object scope, bool checkOnly = false) {
				NonStandard.Data.Parse.SyntaxTree conditionalLogic = fieldToProcess.GetAsSyntaxNode();
				if (conditionalLogic != null && conditionalLogic.rules == CodeRules.IfStatement) {
					if (!checkOnly) { return true; }
					//fieldToProcess.Resolve(errLog, scope, ResolveToNonIfStatement);
					object result = conditionalLogic.Resolve(errLog, scope, ResolveToNonIfStatement);
					if (errLog != null && errLog.HasError()) { return false; }
					fieldToProcess = (Token)result;
					return true;
				}
				return false;
			}
			public void RefreshEditPath(bool preprocessingHeader, object rowObject, ITokenErrLog errLog) {
				// the field can have an editPath if it doesn't contain any binary operators except for member operators
				Token fieldToProcess = _fieldToken;
				if (ResolveConditionalFilter(ref fieldToProcess, errLog, rowObject, preprocessingHeader) && preprocessingHeader) {
					// if this is just pre-processing the header, we know enough that we need to recalculate this field each time. that's enough.
					mustReEvauluateFieldBecauseOfConditionalLogic = true;
					editPath = null;
					return;
				}
				if (errLog != null && errLog.HasError()) { return; }
				List<Token> allTokens = new List<Token>();
				bool isValidEditableField = TokenOnlyContainsSyntax(fieldToProcess, new ParseRuleSet[] { 
					CodeRules.Expression, CodeRules.MembershipOperator, CodeRules.SquareBrace }, allTokens);
				//StringBuilder sb = new StringBuilder();
				//sb.Append(fieldToken.GetAsSmallText() + " " + isValidEditableField + "\n");
				//Show.Log(sb);
				editPath = null;
				if (!isValidEditableField) {
					canEdit = false;
					needsToLoadEditPath = false;

					// TODO go through all existing columns and check if they edit variables that this field depends on

					return;
				}
				canEdit = true;
				needsToLoadEditPath = true;
				//sb.Clear();
				editPath = new List<object>();
				for(int i = 0; i < allTokens.Count; ++i) {
					NonStandard.Data.Parse.SyntaxTree syntax = allTokens[i].meta as NonStandard.Data.Parse.SyntaxTree;
					if (syntax != null) continue;
					editPath.Add(allTokens[i]);
					//sb.Append(allTokens[i].GetAsSmallText()+"\n");
				}
				//Show.Log(sb);

				// TODO remember the raw editPath (it it can be checked by other columns that might depend on this). check if any existing columns depend on this value. check out how BurlyHashTable does this.		
			}
			/// <summary>
			/// asserts that the given token only contains the given valid parser rules
			/// </summary>
			/// <param name="token"></param>
			/// <param name="validParserRules"></param>
			/// <param name="allTokens">if not null, will put all tokens within the main token argument into this list as per <see cref="Token.FlattenInto(List{Token})"/></param>
			/// <returns></returns>
			public bool TokenOnlyContainsSyntax(Token token, IList<ParseRuleSet> validParserRules, List<Token> allTokens = null) {
				if (allTokens == null) allTokens = new List<Token>();
				token.FlattenInto(allTokens);
				for (int i = 0; i < allTokens.Count; ++i) {
					NonStandard.Data.Parse.SyntaxTree syntax = allTokens[i].meta as NonStandard.Data.Parse.SyntaxTree;
					if (syntax == null) continue;
					if (validParserRules.IndexOf(syntax.rules) < 0) { return false; }
				}
				return true;
			}
			public ColumnSetting(DataSheetBase<MetaData> dataSheet) { this.dataSheet = dataSheet; }
		}

		public DataSheetBase() { }

		/// <param name="row"></param>
		/// <param name="col"></param>
		/// <param name="errLog">if null, an exception will be thrown if there is a problem parsing column data</param>
		/// <returns></returns>
		public object RefreshValue(int row, int col, ITokenErrLog errLog = null) {
			if ((col < 0 || col >= columnSettings.Count) && errLog != null) {
				errLog.AddError(-1, "incorrect column: " + col + "\nlimit [0, " + columnSettings.Count + ")");
				return null;
			}
			if ((row < 0 || row >= rows.Count) && errLog != null) {
				errLog.AddError(-1, "incorrect row: " + row + "\nlimit [0, " + rows.Count + ")");
				return null;
			}
			try {
				object value = columnSettings[col].GetValue(errLog, rows[row].obj);
				if (errLog.HasError()) { return null; }
				rows[row].columns[col] = value;
				return value;
			} catch (Exception e) {
				string errorMessage = "could not set [" + row + "," + col + "]: " + e.ToString();
				if (errLog != null) { errLog.AddError(-1, errorMessage); } else {
					throw new Exception(errorMessage);
				}
			}
			return null;
		}
		public void RefreshRowData(RowData rd, List<ColumnSetting> columnSettings, ITokenErrLog errLog = null) {
			for (int i = 0; i < rd.columns.Length; i++) {
				try {
					object value = columnSettings[i].GetValue(errLog, rd.obj);
					rd.columns[i] = value;
				} catch (Exception e) {
					string errorMessage = "could not set (" + columnSettings[i].editPath + "): " + e.ToString();
					if (errLog != null) { errLog.AddError(-1, errorMessage); } else {
						throw new Exception(errorMessage);
					}
				}
			}
		}
		public void RefreshAll(ITokenErrLog errLog = null) {
			for(int r = 0; r < rows.Count; ++r) {
				for (int c = 0; c < columnSettings.Count; ++c) {
					object value = columnSettings[c].GetValue(errLog, rows[r].obj);
					if (errLog != null && errLog.HasError()) { return; }
					rows[r].columns[c] = value;
				}
			}
		}
		public object Get(int row, int col) { return rows[row].columns[col]; }
		public void Set(int row, int col, object value) {
			RowData rd = rows[row];
			rd.columns[col] = value;
		}
		/// <summary>
		/// value at the spreadsheet's given row/col
		/// </summary>
		public object this [int row, int col] { get => Get(row, col); set => Set(row, col, value); }
		/// <summary>
		/// value at the spreadsheet's given row/col
		/// </summary>
		public object this[Coord coord] { get => Get(coord.row, coord.col); set => Set(coord.row, coord.col, value); }
		/// <summary>
		/// the object being represented by the given row
		/// </summary>
		public object this [int row] { get => GetItem(row);
			set {
				RowData rd = rows[row];
				rd.obj = value;
				TokenErrorLog err = new TokenErrorLog();
				AssignData(rd, err);
				if (err.HasError()) { throw new Exception("tokenization error: "+err.GetErrorString()); }
			}
		}
		public object GetItem(int row) { return rows[row].obj; }
		public int IndexOf(object dataModel) { return rows.FindIndex(rd => rd.obj == dataModel); }
		public int IndexOf(Func<object, bool> predicate) {
			for (int i = 0; i < rows.Count; ++i) { if (predicate(rows[i])) { return i; } }
			return -1;
		}

		/// <param name="column">which column is having it's sort statechanged</param>
		/// <param name="sortState">the new <see cref="SortState"/> for this column</param>
		public void SetSortState(int column, SortState sortState) {
			int columnImportance = columnSortOrder.IndexOf(column);
			if (columnImportance >= 0) { columnSortOrder.RemoveAt(columnImportance); }
			columnSettings[column].sortState = sortState;
			if (sortState == SortState.None) {
				columnSortOrder.Remove(column);
				return;
			}
			columnSortOrder.Insert(0, column);
			Sort();
		}

		public void InitData(IList<object> source, ITokenErrLog errLog) {
			rows = new List<RowData>();
			InsertRange(0, source, errLog);
		}
		public void Clear() {
			rows.Clear();
		}

		public void InsertRange(int index, IList<object> source, ITokenErrLog errLog) {
			RowData[] newRows = new RowData[source.Count];
			for (int i = 0; i < source.Count; ++i) {
				newRows[i] = GenerateRow(source[i], errLog);
			}
			rows.InsertRange(index, newRows);
		}
		public void AddRange(IList<object> source, ITokenErrLog errLog) { InsertRange(rows.Count, source, errLog); }
		public RowData AddRow(object elementForRow, ITokenErrLog errLog) {
			RowData rd = GenerateRow(elementForRow, errLog);
			rows.Add(rd);
			return rd;
		}
		public RowData AddRow(object model) { return AddRow(model, null); }
		public RowData InsertRow(int index, object model) {
			RowData rd = GenerateRow(model, null);
			rows.Insert(index, rd);
			return rd;
		}
		/// <param name="source"></param>
		/// <param name="errLog">if null, an exception will be thrown if there is a problem parsing column data</param>
		/// <returns></returns>
		public RowData GenerateRow(object source, ITokenErrLog errLog = null) {
			RowData rd = new RowData(source, null);
			AssignData(rd, errLog);
			return rd;
		}
		/// <summary>
		/// can't put this into <see cref="RowData"/> class because of Generics ambiguity
		/// </summary>
		/// <param name="rd"></param>
		/// <param name="errLog">if null, an exception will be thrown if there is a problem parsing column data</param>
		internal void AssignData(RowData rd, ITokenErrLog errLog = null) {
			if (rd.columns == null || rd.columns.Length != columnSettings.Count) {
				rd.columns = new object[columnSettings.Count];
			}
			bool errNeedsToBeNoisy = false;
			if (errLog == null) { errLog = new Tokenizer(); errNeedsToBeNoisy = true; }
			for (int i = 0; i < columnSettings.Count; ++i) {
				object value = columnSettings[i].GetValue(errLog, rd.obj);
				if (errNeedsToBeNoisy && errLog.HasError()) {
					throw new Exception("error parsing "+columnSettings[i].fieldToken.GetAsSmallText()+":"+errLog.GetErrorString());
				}
				if (value is CodeRules.DefaultString && columnSettings[i].defaultValue != null) {
					value = columnSettings[i].defaultValue;
				}
				rd.columns[i] = value;
			}
		}
		public static int DefaultSorter(object a, object b) {
			if (a == b) { return 0; }
			if (a == null && b != null) { return 1; }
			if (a != null && b == null) { return -1; }
			Type ta = a.GetType(), tb = b.GetType();
			if (CodeConvert.IsNumeric(ta)) { CodeConvert.Convert(ref a, ta = typeof(double)); }
			if (CodeConvert.IsNumeric(tb)) { CodeConvert.Convert(ref b, tb = typeof(double)); }
			if (ta == tb) {
				if (ta == typeof(double)) { return Comparer<double>.Default.Compare((double)a, (double)b); }
				if (ta == typeof(string)) { return StringComparer.Ordinal.Compare(a, b); }
			}
			return 0;
		}

		public int RowSorter(RowData rowA, RowData rowB) {
			for (int i = 0; i < columnSortOrder.Count; ++i) {
				int index = columnSortOrder[i];
				if (columnSettings[index].sortState == SortState.None) {
					//Show.Log("SortState not being set...");
					continue;
				}
				Comparison<object> sort = columnSettings[index].sort;
				if (sort == null) { sort = DefaultSorter; }
				int comparison = sort.Invoke(rowA.columns[index], rowB.columns[index]);
				//Show.Log(comparison+" compare " + rowA.columns[index]+" vs "+ rowB.columns[index]+"   " + rowA.columns[index].GetType() + " vs " + rowB.columns[index].GetType());
				if (comparison == 0) { continue; }
				if (columnSettings[index].sortState == SortState.Descening) { comparison *= -1; }
				return comparison;
			}
			return 0;
		}

		public bool Sort() {
			if (columnSortOrder == null || columnSortOrder.Count == 0) { return false; }
			//Show.Log("SORTING "+columnSortOrder.JoinToString(", ",i=>i.ToString()+":"+columnSettings[i].sortState));
			//StringBuilder sb = new StringBuilder(); sb.Append(rows.JoinToString(",", r => r.model.ToString()) + "\n");
			rows.Sort(RowSorter);
			//sb.Append(rows.JoinToString(",", r => r.model.ToString()) + "\n"); Show.Log(sb);
			return true;
		}

		public void SetColumn(int index, ColumnSetting column) {
			bool newColumn = index >= columnSettings.Count;
			while (columnSettings.Count <= index) { columnSettings.Add(new ColumnSetting(this)); }
			if (!newColumn) {
				Show.Log("TODO convert old column data to new column data");
				//ColumnSetting oldColumn = columnSettings[index];
			}
			columnSettings[index] = column;
			for (int r = 0; r < rows.Count; ++r) {
				RowData rd = rows[r];
				if (columnSettings.Count != rd.columns.Length) {
					AssignData(rd);
				} else {
					RefreshValue(r, index);
				}
			}
		}
		public void AddColumn(ColumnSetting column) { SetColumn(columnSettings.Count, column); }
		public ColumnSetting GetColumn(int index) { return columnSettings[index]; }
		/// <summary>
		/// removes the column by it's index, also removing each corresponding element from each row
		/// </summary>
		/// <param name="columnIndex"></param>
		public void RemoveColumn(int columnIndex) {
			if(columnIndex < 0 || columnIndex >= columnSettings.Count) {
				throw new IndexOutOfRangeException(columnIndex+" OOB, should be [0, "+columnSettings.Count+")");
			}
			columnSettings.RemoveAt(columnIndex);
			for (int r = 0; r < rows.Count; ++r) {
				RowData rd = rows[r];
				object[] newColumns = new object[columnSettings.Count];
				for (int i = 0; i < columnIndex; ++i) { newColumns[i] = rd.columns[i]; }
				for (int i = columnIndex; i < newColumns.Length; ++i) { newColumns[i] = rd.columns[i+1]; }
				rd.columns = newColumns;
			}
		}
		public void RemoveRow(int index) {
			rows.RemoveAt(index);
		}
		public void MoveColumn(int oldIndex, int newIndex) {
			if (oldIndex == newIndex) return;
			// change the index of the column in the header
			ColumnSetting cs = columnSettings[oldIndex];
			columnSettings.RemoveAt(oldIndex);
			columnSettings.Insert(newIndex, cs);
			// go through each data element and change that data
			for (int r = 0; r < rows.Count; ++r) {
				object[] cols = rows[r].columns;
				object valueToMove = cols[oldIndex];
				if (newIndex < oldIndex) {
					// shift elements forward (iterating backward), starting with the old index
					for (int c = oldIndex; c > newIndex; --c) { cols[c] = cols[c - 1]; }
				} else if (newIndex > oldIndex) {
					// shift elements backward (iterating forward), starting with the old index
					for (int c = oldIndex; c < newIndex; ++c) { cols[c] = cols[c + 1]; }
				}
				cols[newIndex] = valueToMove;
			}
			// and get columnSortOrder working correctly as well.
			if (oldIndex < newIndex) {
				for(int i = 0; i < columnSortOrder.Count; ++i) {
					if (columnSortOrder[i] == oldIndex) { columnSortOrder[i] = newIndex; } else
					if (columnSortOrder[i] >  oldIndex && columnSortOrder[i] < newIndex) {
						columnSortOrder[i] = columnSortOrder[i] - 1;
					}
				}
			} else if (oldIndex > newIndex) {
				for (int i = 0; i < columnSortOrder.Count; ++i) {
					if (columnSortOrder[i] == oldIndex) { columnSortOrder[i] = newIndex; } else
					if (columnSortOrder[i] > newIndex && columnSortOrder[i] < oldIndex) {
						columnSortOrder[i] = columnSortOrder[i] + 1;
					}
				}
			}
		}
		public void MoveRow(int oldIndex, int newIndex) {
			if (oldIndex == newIndex) return;
			RowData valueToMove = rows[oldIndex];
			if (newIndex < oldIndex) {
				// shift elements forward (iterating backward), starting with the old index
				for (int c = oldIndex; c > newIndex; --c) { rows[c] = rows[c - 1]; }
			} else if (newIndex > oldIndex) {
				// shift elements backward (iterating forward), starting with the old index
				for (int c = oldIndex; c < newIndex; ++c) { rows[c] = rows[c + 1]; }
			}
			rows[newIndex] = valueToMove;
		}
	}
}
