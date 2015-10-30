﻿using System;
using System.Linq;
using Path = System.IO.Path;
using System.Collections.Generic;
using System.Runtime.Serialization;
using UnityEngine;
using UnityEditor;
using GPUNoise;


namespace GPUNoise.Editor
{
	public class EditorGraphWindow : EditorWindow
	{
		[UnityEditor.MenuItem("GPU Noise/Show Editor")]
		public static void ShowEditor()
		{
			UnityEditor.EditorWindow.GetWindow(typeof(EditorGraphWindow));
		}

		private static readonly float OutputHeight = 30.0f;
		private static readonly float TitleBarHeight = 30.0f,
									  InputSpacing = 20.0f;


		public EditorGraph Editor = null;

		private int selectedGraph = -1;

		private List<string> GraphPaths;
		private GUIContent[] graphSelections;

		private long reconnectingOutput = -1,
					 reconnectingInput = -2;
		private int reconnectingInput_Index = 0;

		private static readonly Vector2 MinLeftSize = new Vector2(100.0f, 100.0f),
										MinGraphSIze = new Vector2(500.0f, 500.0f);

		private Rect WindowRect { get { return new Rect(0.0f, 0.0f, position.width - MinLeftSize.x, position.height); } }


		void OnEnable()
		{
			wantsMouseMove = true;

			GraphPaths = GPUNoise.Applications.GraphUtils.GetAllGraphsInProject();

			Func<string, GUIContent> selector = (s => new GUIContent(Path.GetFileNameWithoutExtension(s), s));
			graphSelections = GraphPaths.Select(selector).ToArray();
			
			selectedGraph = -1;

			minSize = new Vector2(MinLeftSize.x + MinGraphSIze.x,
								  Mathf.Max(MinLeftSize.y, MinGraphSIze.y));
		}

		void OnGUI()
		{
			const float leftSpace = 200.0f;

			GUILayout.BeginArea(new Rect(0, 0, leftSpace, position.height));

			GUILayout.Space(10.0f);

			if (Editor != null)
				GUILayout.Label(Path.GetFileNameWithoutExtension(Editor.FilePath));

			GUILayout.Space(10.0f);

			int oldVal = selectedGraph;
			selectedGraph = EditorGUILayout.Popup(selectedGraph, graphSelections);
			if (selectedGraph != oldVal)
			{
				Editor = new EditorGraph(GraphPaths[selectedGraph], WindowRect);
			}

			GUILayout.Space(50.0f);

			if (Editor != null && GUILayout.Button("Save Changes"))
			{
				Editor.Resave();
			}

			GUILayout.EndArea();

			GUIUtil.DrawLine(new Vector2(leftSpace + 10.0f, 0.0f), new Vector2(leftSpace + 10.0f, position.height), 5.0f, Color.black);


			if (Editor == null)
				return;


			GUILayout.BeginArea(new Rect(leftSpace, 0, position.width - leftSpace, position.height));
			BeginWindows();

			long[] keys = Editor.FuncCallPoses.Keys.ToArray();
			foreach (long uid in keys)
			{
				FuncCall call = (uid == -1 ? new FuncCall(-1, null, new FuncInput[0]) :
											 Editor.GPUGraph.UIDToFuncCall[uid]);
				Editor.FuncCallPoses[uid] = GUINode(Editor.FuncCallPoses[uid], call);
			}

			EndWindows();
			GUILayout.EndArea();
		}
		private Rect GUINode(Rect nodeRect, FuncCall node)
		{
			if (node.UID > int.MaxValue)
			{
				Debug.LogError("UID of " + node.UID + " is too big!");
			}

			string name = (node.Calling == null ? "Output" : node.Calling.Name);
			nodeRect = GUILayout.Window((int)node.UID, nodeRect, GUINodeWindow, name,
										GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

			return nodeRect;
		}
		private void GUINodeWindow(int windowID)
		{
			long uid = (long)windowID;
			Rect r = Editor.FuncCallPoses[uid];

			if (uid == -1)
			{
				GUILayout.BeginVertical();

				GUILayout.BeginHorizontal();

				//Button to connect input to an output.
				string buttStr = (reconnectingInput == -1 ? "x" : "X");
				if (GUILayout.Button(buttStr))
				{
					if (reconnectingOutput >= 0)
					{
						Editor.GPUGraph.Output = new FuncInput(reconnectingOutput);
						reconnectingOutput = -1;
					}
					else
					{
						reconnectingInput = -1;
						reconnectingInput_Index = 0;
					}
				}
				FuncInput graphOut = Editor.GPUGraph.Output;
				if (graphOut.IsAConstantValue)
				{
					Editor.GPUGraph.Output = new FuncInput(EditorGUILayout.FloatField(graphOut.ConstantValue));
				}
				else
				{
					if (GUILayout.Button("Disconnect"))
					{
						Editor.GPUGraph.Output = new FuncInput(0.5f);
						reconnectingInput = -2;
						reconnectingOutput = -1;
					}

					Rect inR = Editor.FuncCallPoses[graphOut.FuncCallID];
					Vector2 endPos = new Vector2(inR.xMax, inR.yMin + OutputHeight) - r.min;
					GUIUtil.DrawLine(new Vector2(0.0f, r.height * 0.5f), endPos, 4.0f, Color.red);
				}

				GUILayout.EndHorizontal();

				GUILayout.EndVertical();
			}
			else
			{
				GUILayout.BeginHorizontal();

				GUILayout.BeginVertical();

				FuncCall fc = Editor.GPUGraph.UIDToFuncCall[uid];
				for (int i = 0; i < fc.Inputs.Length; ++i)
				{
					GUILayout.BeginHorizontal();

					GUILayout.Label(fc.Calling.Params[i].Name);

					//Button to connect input to an output.
					string buttStr = "X";
					if (reconnectingInput == uid && reconnectingInput_Index == i)
					{
						buttStr = "x";
					}
					if (GUILayout.Button(buttStr))
					{
						if (reconnectingOutput >= 0)
						{
							fc.Inputs[i] = new FuncInput(reconnectingOutput);
							reconnectingOutput = -1;
						}
						else
						{
							reconnectingInput = uid;
							reconnectingInput_Index = i;
						}
					}
					if (fc.Inputs[i].IsAConstantValue)
					{
						fc.Inputs[i] = new FuncInput(EditorGUILayout.FloatField(fc.Inputs[i].ConstantValue));
					}
					else
					{
						Rect inR = Editor.FuncCallPoses[fc.Inputs[i].FuncCallID];
						Vector2 endPos = new Vector2(inR.xMax, inR.yMin + OutputHeight) - r.min;

						GUIUtil.DrawLine(new Vector2(0.0f, TitleBarHeight + ((float)i * InputSpacing)),
										 endPos,
										 2.0f, Color.white);

						if (GUILayout.Button("Disconnect"))
						{
							fc.Inputs[i] = new FuncInput(fc.Calling.Params[i].DefaultValue);
							reconnectingInput = -2;
							reconnectingOutput = -1;
						}
					}

					GUILayout.EndHorizontal();
				}

				fc.Calling.CustomGUI(fc.CustomDat);

				GUILayout.EndVertical();

				GUILayout.BeginVertical();

				//Output button.
				string buttonStr = "O";
				if (reconnectingOutput == uid)
					buttonStr = "o";
				if (GUILayout.Button(buttonStr))
				{
					if (reconnectingInput < -1)
					{
						reconnectingOutput = uid;
					}
					else
					{
						if (reconnectingInput == -1)
						{
							Editor.GPUGraph.Output = new FuncInput(uid);
						}
						else
						{
							FuncInput[] inputs = Editor.GPUGraph.UIDToFuncCall[reconnectingInput].Inputs;
							inputs[reconnectingInput_Index] = new FuncInput(uid);
						}

						reconnectingInput = -2;
					}
				}

				GUILayout.EndVertical();

				GUILayout.EndHorizontal();
			}

			GUI.DragWindow();
		}
	}
}