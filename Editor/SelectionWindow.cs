/*
 * Unity標準の Editor/Selection のデータが閉じるたびに消されていたので、
 * 使い勝手を良くするために拡張するクラス。
 */

using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Collections.Generic;

namespace kakunpc.SelectionWindow
{
    public class SelectionWindow : EditorWindow
    {
        [MenuItem("Window/SelectionWindow")]
        public static void ShowWindow()
        {
            var window = GetWindow(typeof(SelectionWindow));
            window.titleContent.text = "SelectionWindow";
        }

        private static void OnSceneGui(SceneView sceneView)
        {
            if (string.IsNullOrEmpty(_notificationText)) return;
            
            sceneView.ShowNotification(new GUIContent(_notificationText));
            _notificationText = string.Empty;
        }

        private static string LibraryPath => System.IO.Path.GetDirectoryName(Application.dataPath) + "/Library/";
        private const string SettingsPath = "UserSelectionSetting.asset";

        private static List<SelectionSetting> _selectionData = null;

        private Vector2 _pos = Vector2.zero;

        private static string _notificationText = string.Empty;

        private void OnEnable()
        {
#if UNITY_2019_1_OR_NEWER
            SceneView.duringSceneGui += OnSceneGui;
#else
            SceneView.onSceneGUIDelegate += OnSceneGUI;
#endif
        }

        private void OnDisable()
        {
#if UNITY_2019_1_OR_NEWER
            SceneView.duringSceneGui -= OnSceneGui;
#else
            if (SceneView.onSceneGUIDelegate != null)
            {
                SceneView.onSceneGUIDelegate -= OnSceneGUI;
            }
#endif
        }

        private void OnGUI()
        {
            if (_selectionData == null)
            {
                Load();
            }

            GUILayout.BeginVertical();
            _pos = GUILayout.BeginScrollView(_pos);

            if (_selectionData != null)
            {
                for (var i = 0; i < _selectionData.Count; ++i)
                {
                    var data = _selectionData[i];
                    GUILayout.BeginHorizontal();
                    string path = "", fileName = "";

                    void JumpButton(float width)
                    {
                        DisabledGroup(string.IsNullOrEmpty(data.GUID), () =>
                        {
                            if (GUILayout.Button("飛", GUILayout.Width(width)))
                            {
                                var instanceId = GetInstanceIdFromGuid(data.GUID);
                                Selection.activeInstanceID = instanceId;
                                _notificationText = "JUMP:" + fileName;
                            }
                        });
                    }

                    if (string.IsNullOrEmpty(data.GUID) == false)
                    {
                        path = AssetDatabase.GUIDToAssetPath(data.GUID);
                        fileName = System.IO.Path.GetFileName(path);
                        var ext = System.IO.Path.GetExtension(path);

                        // シーン開く
                        if (ext == ".unity")
                        {
                            DisabledGroup(EditorApplication.isPlaying, () =>
                            {
                                if (GUILayout.Button("開", GUILayout.Width(26f)))
                                {
                                    var open = true;
                                    var activeScene = EditorSceneManager.GetActiveScene();
                                    if (activeScene.isDirty)
                                    {
                                        open = EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
                                    }

                                    if (open)
                                    {
                                        EditorSceneManager.OpenScene(path);
                                        _notificationText = "Open:" + fileName;
                                    }
                                }
                            });
                            GUILayout.Space(3);
                            JumpButton(26f);
                        }

                        // ソースファイルを開く
                        /* else if (Ext == ".cs" || Ext == ".js")
                        {
                            if (GUILayout.Button("開", GUILayout.Width(26f)))
                            {
                                UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(path, 1);
                                _notificationText = "Open:" + fileName;
                            }
                            GUILayout.Space(3);
                            jumpButton(26f);
                        }*/
                        else
                        {
                            JumpButton(60f);
                        }
                    }
                    else
                    {
                        DisabledGroup(!CheckSelection(), () =>
                        {
                            if (GUILayout.Button("SET", GUILayout.Width(60f)))
                            {
                                var newPath = AssetDatabase.GetAssetPath(Selection.activeInstanceID);
                                var newGuid = AssetDatabase.AssetPathToGUID(newPath);
                                data.GUID = newGuid;
                                Save();
                            }
                        });
                    }

                    GUILayout.Label($"[{data.number}]{(string.IsNullOrEmpty(fileName) ? "Empty" : fileName)}", GUILayout.MinWidth(150f), GUILayout.MaxWidth(200f));

                    DisabledGroup(string.IsNullOrEmpty(data.GUID), () =>
                    {
                        if (GUILayout.Button("X", GUILayout.Width(20f)))
                        {
                            data.Reset();
                            _selectionData[i] = data;
                            Save();
                        }
                    });

                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private static void Save()
        {
            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(List<SelectionSetting>));
            using (var sw = new System.IO.StreamWriter(LibraryPath + SettingsPath, false, new System.Text.UTF8Encoding(false)))
            {
                serializer.Serialize(sw, _selectionData);
            }
        }

        static void Load()
        {
            if (System.IO.File.Exists(LibraryPath + SettingsPath))
            {
                var serializer = new System.Xml.Serialization.XmlSerializer(typeof(List<SelectionSetting>));
                using (var sr = new System.IO.StreamReader(LibraryPath + SettingsPath, new System.Text.UTF8Encoding(false)))
                {
                    var saveData = (List<SelectionSetting>)serializer.Deserialize(sr);
                    _selectionData = new List<SelectionSetting>();
                    foreach (var save in saveData)
                    {
                        _selectionData.Add(save);
                    }
                }
            }
            else
            {
                _selectionData = new List<SelectionSetting>();
            }

            for (var i = 0; i < 10; ++i)
            {
                if (_selectionData.Exists(x => x.number == i) == false)
                {
                    _selectionData.Add(new SelectionSetting(i, string.Empty));
                }
            }

            _selectionData.Sort((x, y) => { return x.number - y.number; });
        }

        private static void DisabledGroup(bool state, Action gropuAction)
        {
            EditorGUI.BeginDisabledGroup(state);
            gropuAction();
            EditorGUI.EndDisabledGroup();
        }

        private static void Select(int id)
        {
            Load();

            var find = _selectionData.Find(x => x.number == id);
            if (find != null &&
                string.IsNullOrEmpty(find.GUID) == false)
            {
                Selection.activeInstanceID = GetInstanceIdFromGuid(find.GUID);
            }
        }

        private static void SaveSelection(int id)
        {
            Load();

            var path = AssetDatabase.GetAssetPath(Selection.activeInstanceID);
            var GUID = AssetDatabase.AssetPathToGUID(path);

            var find = _selectionData.Find(x => x.number == id);
            if (find != null)
            {
                find.GUID = GUID;
            }
            else
            {
                _selectionData.Add(new SelectionSetting(id, GUID));
            }

            Save();
        }

        private static bool CheckSelection(int id)
        {
            Load();
            return _selectionData.Exists(x => x.number == id);
        }

        private static bool CheckSelection()
        {
            return Selection.activeInstanceID != 0;
        }

        private static int GetInstanceIdFromGuid(string guid)
        {
            return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(AssetDatabase.GUIDToAssetPath(guid)).GetInstanceID();
        }

    }

    public class SelectionSetting
    {
        public int number;
        public string GUID;

        public SelectionSetting()
        {
            number = -1;
            Reset();
        }

        public SelectionSetting(int number, string id)
        {
            this.number = number;
            GUID = id;
        }

        public void Reset()
        {
            GUID = string.Empty;
        }
    }

}
