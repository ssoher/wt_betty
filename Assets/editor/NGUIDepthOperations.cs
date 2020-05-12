/**/

namespace AE.Editor {
	using UnityEngine;
	using UnityEditor;

	/// <summary>
	/// Set's the draw orders in the selection consecutively
	/// </summary>
	internal sealed class NGUIDepthOperations : EditorWindow {
		private static int  startDepth;
		private static bool isIgnoreNegative = true;

		private static int searchDepth;

		private Vector2 scrollPos;

		[MenuItem("Accidental Empire/NGUI Draw Depth Operations")]
		private static void InitWindow() {
			var hwnd = GetWindow<NGUIDepthOperations>();
			hwnd.Show();
		}

		private void OnGUI() { DrawGUI(); }

		private void DrawGUI() {
			scrollPos = GUILayout.BeginScrollView(scrollPos);

			GUILayout.Label("Select any part of your UI as a starting point before any of the operations\n" +
							"All the operations are recursive taking the active selection as the root parent");

			GUILayout.BeginVertical("Groupbox");

			GUI.contentColor = Color.red;
			GUILayout.Label("SET DRAW ORDERS", "PreLabel");
			GUI.contentColor = Color.white;

			// Starting draw order index
			startDepth = EditorGUILayout.IntField("Starting Depth", startDepth);

			// Ignore negatives toggle
			GUILayout.BeginHorizontal();
			GUILayout.Label(new GUIContent("Ignore Negatives(?)",
										   "Doesn't assign a depth value if the widget in question has a negative depth value set"),
							GUILayout.MaxWidth(145f));

			if (GUILayout.Button(isIgnoreNegative ? "√" : string.Empty, GUILayout.MaxWidth(20f))) {
				if (isIgnoreNegative) {
					if (EditorUtility.DisplayDialog("Ignore Negatives",
													"Disabling this option will change the value of negatives when depths are ordered",
													"OK",
													"Cancel"))
						isIgnoreNegative = false;
				} else
					isIgnoreNegative = true;
			}

			GUILayout.EndHorizontal();

			// Order buttons
			if (GUILayout.Button("Start Widget Order Process")) SetWidgetDrawOrders(startDepth);
			if (GUILayout.Button("Start Panel Order Process")) SetPanelDrawOrders(startDepth);

			GUILayout.EndVertical();

			GUILayout.BeginVertical("Groupbox");
			GUI.contentColor = Color.red;
			GUILayout.Label("SEARCH WIDGET", "PreLabel");
			GUI.contentColor = Color.white;

			GUILayout.BeginHorizontal();
			searchDepth = EditorGUILayout.IntField("Search depth", searchDepth);
			if (GUILayout.Button("Find Depth")) FindWidgetWithDepthRecursively(searchDepth, Selection.activeTransform);
			GUILayout.EndHorizontal();
			GUILayout.Space(20f);

			if (GUILayout.Button("Find Lowest Depth")) FindLowestDepthWidget(Selection.activeTransform);
			GUILayout.Space(20f);

			if (GUILayout.Button("Find Highest Depth")) FindHighestDepthWidget(Selection.activeTransform);

			GUILayout.EndVertical();

			GUILayout.BeginVertical("Groupbox");
			GUI.contentColor = Color.red;
			GUILayout.Label("SEARCH PANEL", "PreLabel");
			GUI.contentColor = Color.white;

			if (GUILayout.Button("Find Last Panel")) FindHighestDepthPanel(Selection.activeTransform);

			GUILayout.EndVertical();

			GUILayout.EndScrollView();
		}

		private static void SetWidgetDrawOrders(int startingDepth) {
			var workDepth = startingDepth;
			SetWidgetDrawOrderRecursively(ref workDepth, Selection.activeTransform);
			Debug.Log($"Done depth ordering {workDepth} NGUI widgets");
		}

		private static void SetPanelDrawOrders(int startingDepth) {
			var workDepth = startingDepth;
			SetPanelDrawOrderRecursively(ref workDepth, Selection.activeTransform);
			Debug.Log($"Done depth ordering {workDepth}  NGUI panels");
		}

		private static void SetWidgetDrawOrderRecursively(ref int depth, Transform startTransform) {
			var widget = startTransform.gameObject.GetComponent<UIWidget>();

			if (widget) {
				if (!isIgnoreNegative || (isIgnoreNegative && widget.depth >= 0)) {
					widget.depth = depth;
					depth++;
				}
			}

			foreach (Transform selectionIter in startTransform) SetWidgetDrawOrderRecursively(ref depth, selectionIter);
		}

		private static void SetPanelDrawOrderRecursively(ref int depth, Transform startTransform) {
			var panel = startTransform.gameObject.GetComponent<UIPanel>();

			if (panel) {
				if (!isIgnoreNegative || (isIgnoreNegative && panel.depth >= 0)) {
					panel.depth = depth;
					depth++;
				}
			}

			foreach (Transform selectionIter in startTransform) SetPanelDrawOrderRecursively(ref depth, selectionIter);
		}

		private static void FindWidgetWithDepthRecursively(int desiredDepth, Transform startTransform) {
			var widget = startTransform.gameObject.GetComponent<UIWidget>();

			if (widget && widget.depth == desiredDepth) {
				Selection.activeObject = widget;
				EditorGUIUtility.PingObject(widget);
				return;
			}

			foreach (Transform selectionIter in startTransform) FindWidgetWithDepthRecursively(desiredDepth, selectionIter);
		}

		private static void FindLowestDepthWidget(Transform startTransform) {
			// Just some high value to be overwritten
			var      lowest       = 1000;
			UIWidget lowestWidget = null;

			var allTransforms = startTransform.GetComponentsInChildren<Transform>(true);

			foreach (var selectionIter in allTransforms) {
				var widget = selectionIter.gameObject.GetComponent<UIWidget>();

				if (!widget || widget.depth >= lowest) continue;

				lowest       = widget.depth;
				lowestWidget = widget;
			}

			if (lowestWidget == null) return;

			Selection.activeObject = lowestWidget;
			EditorGUIUtility.PingObject(lowestWidget);
		}

		private static void FindHighestDepthWidget(Transform startTransform) {
			// Just some low value to be overwritten
			var      highest       = -1000;
			UIWidget highestWidget = null;

			var allTransforms = startTransform.GetComponentsInChildren<Transform>(true);

			foreach (var selectionIter in allTransforms) {
				var widget = selectionIter.gameObject.GetComponent<UIWidget>();

				if (!widget || widget.depth <= highest) continue;

				highest       = widget.depth;
				highestWidget = widget;
			}

			if (highestWidget == null) return;

			Selection.activeObject = highestWidget;
			EditorGUIUtility.PingObject(highestWidget);
		}

		private static void FindHighestDepthPanel(Transform startTransform) {
			// Just some low value to be overwritten
			var     highest      = -1000;
			UIPanel highestPanel = null;

			var allTransforms = startTransform.GetComponentsInChildren<Transform>(true);

			foreach (var selectionIter in allTransforms) {
				var panel = selectionIter.gameObject.GetComponent<UIPanel>();

				if (!panel || panel.depth <= highest) continue;

				highest      = panel.depth;
				highestPanel = panel;
			}

			if (highestPanel == null) return;

			Selection.activeObject = highestPanel;
			EditorGUIUtility.PingObject(highestPanel);
		}
	}
}