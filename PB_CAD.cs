using System;
using BepInEx;
using Logger = BepInEx.Logging.Logger;
using PolyTechFramework;
using UnityEngine;
using HarmonyLib;
using System.Reflection;
using System.Collections.Generic;
using BepInEx.Configuration;
using ConfigurationManager;
using System.Linq;
using UnityEngine.UI;
using System.IO;


namespace PB_CAD
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    // Specify the mod as a dependency of PTF
    [BepInDependency(PolyTechMain.PluginGuid, BepInDependency.DependencyFlags.HardDependency)]
    // This Changes from BaseUnityPlugin to PolyTechMod.
    // This superclass is functionally identical to BaseUnityPlugin, so existing documentation for it will still work.
    public class PB_CAD : PolyTechMod
    {
        public new const string
            PluginGuid = "PolyTech.PB_CAD",
            PluginName = "PB CAD",
            PluginVersion = "0.1.0";

        public static PB_CAD instance;
        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<BepInEx.Configuration.KeyboardShortcut> splitKey;
        public static ConfigEntry<BepInEx.Configuration.KeyboardShortcut> menuKey;		
        public static ConfigEntry<BepInEx.Configuration.KeyboardShortcut> combineKey;
        public static ConfigEntry<BepInEx.Configuration.KeyboardShortcut> muscleKey;		
        public static ConfigEntry<BepInEx.Configuration.KeyboardShortcut> intersectKey;

        Harmony harmony;
		
		internal Rect WindowRect { get; private set; }
        internal int LeftColumnWidth { get; private set; }
        internal int RightColumnWidth { get; private set; }
        private bool _displayingWindow = false;
        private Texture2D WindowBackground;
        private int inputWidth = 50;
        public Vector2 scrollPosition;
		
	    public static ConfigEntry<bool>
            logActions,
            showMice;
        public static ConfigEntry<float>
            backupFrequency,
            writeToLogFrequency;
        private static ConfigEntry<BepInEx.Configuration.KeyboardShortcut> 
            _keybind,
            _toggleChatKeybind,
            syncLayoutKeybind;

		
		//GUI Things
		private void OnGUI(){
			if (DisplayingWindow)
			{
				if (Event.current.type == UnityEngine.EventType.KeyUp && Event.current.keyCode == _keybind.Value.MainKey)
				{
					DisplayingWindow = false;
					return;
				}

				GUI.Box(WindowRect, GUIContent.none, new GUIStyle { normal = new GUIStyleState { background = WindowBackground } });
				WindowRect = GUILayout.Window(-69, WindowRect, PB_CADWindow, "Part Halver");
				EatInputInRect(WindowRect);
				}
		}

		private void CalculateWindowRect()
		{
			var width = 200;
			var height = 120;
			var offsetX = Mathf.RoundToInt((Screen.width - width) / 2f);
			var offsetY = Mathf.RoundToInt((Screen.height - height) / 2f);
			WindowRect = new Rect(offsetX, offsetY, width, height);

			LeftColumnWidth = Mathf.RoundToInt(WindowRect.width / 1.5f);
			RightColumnWidth = (int)WindowRect.width - LeftColumnWidth;
		}


		public static void Horizontal(System.Action block, GUIStyle style = null){
			if (style != null) GUILayout.BeginHorizontal(style);
			else GUILayout.BeginHorizontal();
			block();
			GUILayout.EndHorizontal();
		}
		
		public static void Vertical(System.Action block, GUIStyle style = null){
			if (style != null) GUILayout.BeginVertical(style);
			else GUILayout.BeginVertical();
			block();
			GUILayout.EndVertical();
		}

		public static void DrawHeader(string text){
			var _style = new GUIStyle(GUI.skin.label)
			{
				fontSize = 15
			};
			GUILayout.Label(text, _style);
		}

		private void PB_CADWindow(int id){
			try {
				scrollPosition = GUILayout.BeginScrollView(scrollPosition);

				// Settings
				Vertical(() =>
				{
					Horizontal(() => {
						GUILayout.FlexibleSpace();
						DrawHeader("Settings");
						GUILayout.FlexibleSpace();
					});
					Horizontal(() =>
					{
						GUILayout.Label("Number of Parts");
						GUIValues.numParts = GUILayout.TextField(GUIValues.numParts, GUILayout.Width(inputWidth));
					});

					if (GUILayout.Button("Apply Split")){
						InterfaceAudio.Play("ui_menu_select");
						RequestsToApply.splitRequested = true;
					}				


            }, GUI.skin.box);
			GUI.DragWindow();
            GUILayout.EndScrollView();
            }
            catch (ArgumentException ex){
                instance.Logger.LogError(ex.Message);
            }
        }
        
        public static void EatInputInRect(Rect eatRect)
        {
            if (eatRect.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y)))
                Input.ResetInputAxes();
        }

        public bool DisplayingWindow
        {
            get => _displayingWindow;
            set
            {
                if (_displayingWindow == value) return;
                _displayingWindow = value;

                if (_displayingWindow)
                {
                    CalculateWindowRect();
                }
            }
        }

        public static class GUIValues {
			public static string numParts = "2";
        }
		
		public static class RequestsToApply {
			public static bool splitRequested = false;
			public static bool intersectRequested = false;
			public static bool combineRequested = false;
			public static bool muscleReplaceRequested = false;
		}
		
        void Awake()
        {
            //this.repositoryUrl = "https://github.com/Conqu3red/Template-Mod/"; // repo to check for updates from
            if (instance == null) instance = this;
            // Use this if you wish to make the mod trigger cheat mode ingame.
            // Set this true if your mod effects physics or allows mods that you can't normally do.
            isCheat = false;

            modEnabled = Config.Bind("PB CAD", "modEnabled", true, "Enable Mod");
            menuKey = Config.Bind("PB CAD", "Menu Keybind", new BepInEx.Configuration.KeyboardShortcut(KeyCode.M), "Menu");
			splitKey = Config.Bind("PB CAD", "Split Keybind", new BepInEx.Configuration.KeyboardShortcut(KeyCode.N), "Split Keybind");
			combineKey = Config.Bind("PB CAD", "Combine Keybind", new BepInEx.Configuration.KeyboardShortcut(KeyCode.J), "Combine Keybind");
			muscleKey = Config.Bind("PB CAD", "Muscle Replace Keybind", new BepInEx.Configuration.KeyboardShortcut(KeyCode.Q), "Muscle Replace Keybind");
			intersectKey = Config.Bind("PB CAD", "Intersect Keybind", new BepInEx.Configuration.KeyboardShortcut(KeyCode.I), "Intersect Keybind");
			
            modEnabled.SettingChanged += onEnableDisable;
            
            this.isEnabled = modEnabled.Value;

            harmony = new Harmony("PolyTech.PB_CAD");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            this.authors = new string[] { "Piecat" };

            PolyTechMain.registerMod(this);
        }

        public static void RaiseInputError(string error){
            //GUIValues.ConnectionResponse = $"<color=red>Incorrect Input: {error}</color>";
			//TODO
        }


		//Mod Content

        public void Start()
        {
            typeof(BridgeSelectionSet).Assembly.GetType("BridgeActions");
        }

        public void onEnableDisable(object sender, EventArgs e)
        {
            this.isEnabled = modEnabled.Value;

            if (modEnabled.Value)
            {
                enableMod();
            }
            else
            {
                disableMod();
            }
        }
        public override void enableMod()
        {
            modEnabled.Value = true;
        }
        public override void disableMod()
        {
            modEnabled.Value = false;
        }

        public bool shouldRun()
        {
            return PolyTechMain.ptfInstance.isEnabled && this.isEnabled;
        }

        void Update()
        {
			if(shouldRun()) {
				
				//Key Binds
				if(menuKey.Value.IsUp())
				{
					DisplayingWindow = !DisplayingWindow;
				}

                if(intersectKey.Value.IsUp())
                {
                    RequestsToApply.intersectRequested = true;
                }

                if(muscleKey.Value.IsUp()) 
                {
                    RequestsToApply.muscleReplaceRequested = true;
                }

                if(combineKey.Value.IsUp()) 
                {
                    RequestsToApply.combineRequested = true;
                }


				//Part Splitter
				if(RequestsToApply.splitRequested == true)
				{				

					int numParts;
					try {
						numParts = int.Parse(GUIValues.numParts);
					}
					catch {
						RaiseInputError("Number of Parts must be int");
						return;
					}

					// begin undo/redo frame
					PublicBridgeActions.StartRecording();
					
					// do split on every selected edge
					var edges = new HashSet<BridgeEdge>(BridgeSelectionSet.m_Edges);
					
					// do split
					foreach (var edge in edges)
					{
						if (edge.isActiveAndEnabled)
						{
							splitEdge(edge, numParts);

							edge.ForceDisable();
							edge.SetStressColor(0f);

							PublicBridgeActions.Delete(edge);
						}
					}

					BridgeEdges.UpdateManual();
					
					// end undo/redo frame
					PublicBridgeActions.FlushRecording();
					RequestsToApply.splitRequested = false;
				}
				
				if(RequestsToApply.intersectRequested == true) 
				{
				
					// begin undo/redo frame
					PublicBridgeActions.StartRecording();
					
                    //Run the intersect method on all selected items
                    intersectRecursive();

					BridgeEdges.UpdateManual();
					
					// end undo/redo frame
					PublicBridgeActions.FlushRecording();				
				}

                if(RequestsToApply.combineRequested == true) {
                    //TODO

                    //Check if segments are colinear
                    //Combine edges into one
                    //Do we want to check if the length is too long?
                }

                if(RequestsToApply.muscleReplaceRequested == true) {
                    //TODO
                    //Replaces segment(s) with "muscle", or creates if only 2 joints are selected
                }

				RequestsToApply.splitRequested = false;
                RequestsToApply.intersectRequested = false;
				RequestsToApply.combineRequested = false;
                RequestsToApply.muscleReplaceRequested = false;

			}

			
        }
		
		bool intersectHelperCCW(Vector3 A, Vector3 B, Vector3 C) {
            //Returns if ordered points are counter clockwise. Used for checking intersection
			return (C.y-A.y)*(B.x-A.x) > (B.y-A.y)*(C.x-A.x);
		}

        bool intersectHelperChecker(Vector3 A, Vector3 B, Vector3 C, Vector3 D) {
            //Checks if lines are intersecting and do NOT share a joint already

            //Check if any end-points are shared
            if(A.Equals(B) || A.Equals(C) || A.Equals(D) || B.Equals(C) || B.Equals(D) || C.Equals(D)) {
                return false;
            }

            //Check if they are intersecting
            return intersectHelperCCW(A,C,D) != intersectHelperCCW(B,C,D) && intersectHelperCCW(A,B,C) != intersectHelperCCW(A,B,D);
        }

        Vector3 intersectHelperFindPoint(Vector3 A, Vector3 B, Vector3 C, Vector3 D) {
            //Returns Intersection Point (X,Y)

            float a1 = B.y - A.y;
            float b1 = A.x - B.x;
            float c1 = a1 * A.x + b1 * A.y;
    
            float a2 = D.y - C.y;
            float b2 = C.x - D.x;
            float c2 = a2 * C.x + b2 * C.y;

            float delta = a1 * b2 - a2 * b1;

            Vector3 pos = new Vector3((b2 * c1 - b1 * c2) / delta, (a1 * c2 - a2 * c1) / delta, 0f);

            return pos;
        }

        bool intersectRecursive() {
            //This method is recursive. We must re-load the edges and re-check them all after each intersection operation.

            //Get selected edges
            var edgesA = new HashSet<BridgeEdge>(BridgeSelectionSet.m_Edges);
            var edgesB = new HashSet<BridgeEdge>(BridgeSelectionSet.m_Edges);

            //Check all edges against each other
            foreach (var edgeA in edgesA)
			    {
                foreach (var edgeB in edgesB)
	    		    {
                        //Check if edges are distinct
		    			if(edgeA != edgeB) {
                            //Check if lines are even intersecting
                            if(intersectHelperChecker(edgeA.m_JointA.m_BuildPos, edgeA.m_JointB.m_BuildPos, edgeB.m_JointA.m_BuildPos, edgeB.m_JointB.m_BuildPos)) {
                                //We have distinct edges that are intersecting. Now we can call the intersect method which does the split operation
                                intersectMain(edgeA, edgeB);
                                return intersectRecursive();
                            }
                        }
                    }
                //All edges in A have been checked. For efficiency, remove this from the set being checked in B.
                edgesB.Remove(edgeA);
                }

            return true;
        }


        void intersectMain(BridgeEdge edgeA, BridgeEdge edgeB) {
            //Create and delete elements to do the intersection


            //Find intersection point and put a joint there
            Vector3 pos = intersectHelperFindPoint(edgeA.m_JointA.m_BuildPos, edgeA.m_JointB.m_BuildPos, edgeB.m_JointA.m_BuildPos, edgeB.m_JointB.m_BuildPos);

            var newJoint = BridgeJoints.CreateJoint(pos, Guid.NewGuid().ToString());
            PublicBridgeActions.Create(newJoint);

            // make edges for first line, edge A
            var newEdgeA = BridgeEdges.CreateEdgeWithPistonOrSpring(edgeA.m_JointA, newJoint, edgeA.m_Material.m_MaterialType);
            PublicBridgeActions.Create(newEdgeA);
            BridgeSelectionSet.SelectEdge(newEdgeA);

            var newEdgeB = BridgeEdges.CreateEdgeWithPistonOrSpring(edgeA.m_JointB, newJoint, edgeA.m_Material.m_MaterialType);
            PublicBridgeActions.Create(newEdgeB);
            BridgeSelectionSet.SelectEdge(newEdgeB);

            // make edges for first line, edge B
            var newEdgeC = BridgeEdges.CreateEdgeWithPistonOrSpring(edgeB.m_JointA, newJoint, edgeB.m_Material.m_MaterialType);
            PublicBridgeActions.Create(newEdgeC);
            BridgeSelectionSet.SelectEdge(newEdgeC);

            var newEdgeD = BridgeEdges.CreateEdgeWithPistonOrSpring(edgeB.m_JointB, newJoint, edgeB.m_Material.m_MaterialType);
            PublicBridgeActions.Create(newEdgeD);
            BridgeSelectionSet.SelectEdge(newEdgeD);

            //Delete the edges we're replacing
            //We MUST de-select the edge, or we will be stuck in a recursive loop. The wrong number of elements will be created too.
			edgeA.ForceDisable();
			edgeA.SetStressColor(0f);
			PublicBridgeActions.Delete(edgeA);
            BridgeSelectionSet.DeSelectEdge(edgeA);

			edgeB.ForceDisable();
			edgeB.SetStressColor(0f);
			PublicBridgeActions.Delete(edgeB);
            BridgeSelectionSet.DeSelectEdge(edgeB);
            
            return;
        }
		

        void splitEdge(BridgeEdge edge, int numParts)
        {
            var joints = new List<BridgeJoint> { edge.m_JointA };
            
            // make joints
            for (int i = 1; i < numParts; i++)
            {
                Vector3 pos = Vector3.Lerp(edge.m_JointA.m_BuildPos, edge.m_JointB.m_BuildPos, i / (float)numParts);
                //Debug.Log($"{i} - {i / numParts} -> {pos}");
                var newJoint = BridgeJoints.CreateJoint(pos, Guid.NewGuid().ToString());
                PublicBridgeActions.Create(newJoint);
                joints.Add(newJoint);
            }
            joints.Add(edge.m_JointB);

            // make edges
            for (int i = 0; i < joints.Count - 1; i++)
            {
                var newEdge = BridgeEdges.CreateEdgeWithPistonOrSpring(joints[i], joints[i + 1], edge.m_Material.m_MaterialType);
                PublicBridgeActions.Create(newEdge);
                BridgeSelectionSet.SelectEdge(newEdge);
            }
        }
        void splitEdge_in_two(BridgeEdge edge)
        {
            /*
                NOTE: this function splits an edge in two, but is not used, in favour of a generic version
                (see above)
            */
            Vector3 midpoint = (edge.m_JointA.m_BuildPos + edge.m_JointB.m_BuildPos) / 2;

            // create new joint in the middle
            var newJoint = BridgeJoints.CreateJoint(midpoint, Guid.NewGuid().ToString());
            PublicBridgeActions.Create(newJoint);

            // create the 2 new edges
            var edge1 = BridgeEdges.CreateEdgeWithPistonOrSpring(edge.m_JointA, newJoint, edge.m_Material.m_MaterialType);
            PublicBridgeActions.Create(edge1);
            BridgeSelectionSet.DeSelectEdge(edge1);
            
            var edge2 = BridgeEdges.CreateEdgeWithPistonOrSpring(edge.m_JointB, newJoint, edge.m_Material.m_MaterialType);
            PublicBridgeActions.Create(edge2);
            BridgeSelectionSet.DeSelectEdge(edge2);
        }


        /*
           PublicBridgeActions class
           This class exposes the internal BridgeActions class, allowing for modification
           of the undo/redo queue. It's mainly useful for when a mod modifies the bridge.
        */

        class PublicBridgeActions
        {
            public static Type Internal_BridgeActions = typeof(BridgeSelectionSet).Assembly.GetType("BridgeActions");
            public static void StartRecording()
            {
                Internal_BridgeActions.GetMethod("StartRecording", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { });
            }
            public static bool IsRecording()
            {
                return (bool)Internal_BridgeActions.GetMethod("IsRecording", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { });

            }
            public static void FlushRecording()
            {
                Internal_BridgeActions.GetMethod("FlushRecording", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { });
            }
            public static void CancelRecording()
            {
                Internal_BridgeActions.GetMethod("CancelRecording", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { });
            }
            public static void Create(BridgeJoint joint)
            {
                Internal_BridgeActions.GetMethod(
                    "Create",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new Type[] { typeof(BridgeJoint) },
                    null
                ).Invoke(null, new object[] { joint });
            }
            public static void Create(BridgeEdge edge)
            {
                Internal_BridgeActions.GetMethod(
                    "Create",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new Type[] { typeof(BridgeEdge) },
                    null
                ).Invoke(null, new object[] { edge });
            }
            public static void Delete(BridgeJoint joint)
            {
                Internal_BridgeActions.GetMethod(
                    "Delete",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new Type[] { typeof(BridgeJoint) },
                    null
                ).Invoke(null, new object[] {joint});
            }
            public static void Delete(BridgeEdge edge)
            {
                Internal_BridgeActions.GetMethod(
                    "Delete",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new Type[] { typeof(BridgeEdge) },
                    null
                ).Invoke(null, new object[] {edge});
            }
            public static void Create(List<BridgeJoint> joints)
            {
                Internal_BridgeActions.GetMethod(
                    "Create",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new Type[] { typeof(List<BridgeJoint>) },
                    null
                ).Invoke(null, new object[] {joints});
            }
            public static void Create(List<BridgeEdge> edges)
            {
                Internal_BridgeActions.GetMethod(
                    "Create",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new Type[] { typeof(List<BridgeEdges>) },
                    null
                ).Invoke(null, new object[] {edges});
            }
            public static void Delete(List<BridgeJoint> joints)
            {
                Internal_BridgeActions.GetMethod(
                    "Delete",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new Type[] { typeof(List<BridgeJoint>) },
                    null
                ).Invoke(null, new object[] {joints});
            }
            public static void Delete(List<BridgeEdge> edges)
            {
                Internal_BridgeActions.GetMethod(
                    "Delete",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new Type[] { typeof(List<BridgeEdge>) },
                    null
                ).Invoke(null, new object[] {edges});
            }
            public static void Delete(HashSet<BridgeEdge> edges)
            {
                Internal_BridgeActions.GetMethod(
                    "Delete",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new Type[] { typeof(HashSet<BridgeEdge>) },
                    null
                ).Invoke(null, new object[] {edges});
            }
            public static void Translate(BridgeJoint joint, Vector3 translation)
            {
                Internal_BridgeActions.GetMethod("Translate", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] {joint, translation});
            }
            public static void MakeAnchor(BridgeJoint joint)
            {
                Internal_BridgeActions.GetMethod("MakeAnchor", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] {joint});
            }
            public static void TranslatePistonSlider(Piston piston, float translation)
            {
                Internal_BridgeActions.GetMethod("TranslatePistonSlider", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] {piston, translation});
            }
            public static void TranslateSpringSlider(BridgeSpring spring, float translation)
            {
                Internal_BridgeActions.GetMethod("TranslateSpringSlider", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] {spring, translation});
            }
            public static void SplitJoint(BridgeJoint joint)
            {
                Internal_BridgeActions.GetMethod("SplitJoint", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] {joint});
            }
            public static void UnSplitJoint(BridgeJoint joint)
            {
                Internal_BridgeActions.GetMethod("UnSplitJoint", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] {joint});
            }
        }

    }
}