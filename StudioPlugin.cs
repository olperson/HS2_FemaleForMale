using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml;
using AIChara;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Studio;
using UnityEngine;
using UnityEngine.UI;
using StudioCore = Studio.Studio;

namespace Codex.HS2FemaleForMale.StudioSupport
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("StudioNEOV2")]
    [BepInDependency("com.bepis.bepinex.extendedsave", "21.1.2")]
    [BepInDependency("marco.kkapi", "1.45.1")]
    [BepInDependency("com.joan6694.illusionplugins.bonesframework", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.animal42069.additionalfknodes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.deathweasel.bepinex.fkik", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("orange.spork.advikplugin", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.joan6694.illusionplugins.nodesconstraints", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("mikke.Charloader", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.joan6694.illusionplugins.timeline", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("KKABMX.Core", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.joan6694.illusionplugins.poseeditor", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class FemaleForMaleStudioPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.codex.hs2.femaleformale.studio";
        public const string PluginName = "HS2 Studio 女角色完整替换男角色";
        public const string PluginVersion = "1.2.0";

        private static readonly int[] MaleOnlyBoneIds = { 67, 68, 69 };
        private static readonly PropertyInfo SexProperty = AccessTools.Property(typeof(OICharInfo), "sex");
        private static readonly PropertyInfo CharFileProperty = AccessTools.Property(typeof(OICharInfo), "charFile");
        private static readonly FieldInfo CharaListSexField = AccessTools.Field(typeof(CharaList), "sex");
        private static readonly FieldInfo CharaFileSortField = AccessTools.Field(typeof(CharaList), "charaFileSort");
        private static readonly FieldInfo ChangeButtonField = AccessTools.Field(typeof(CharaList), "buttonChange");
        private static readonly FieldInfo GuideObjectDictionaryField =
            AccessTools.Field(typeof(GuideObjectManager), "dicGuideObject");

        private static Type _abmxBoneControllerType;
        private static PropertyInfo _abmxBoneSearcherProperty;
        private static PropertyInfo _abmxNeedsFullRefreshProperty;
        private static PropertyInfo _abmxNeedsBaselineUpdateProperty;
        private static FieldInfo _abmxBoneSearcherControlField;
        private static FieldInfo _abmxBaselineKnownField;
        private static FieldInfo _abmxPreviousAnimSpeedField;
        private static FieldInfo _abmxPartialBaselineTargetsField;
        private static MethodInfo _abmxGetAllModifiersMethod;
        private static PropertyInfo _abmxModifierNameProperty;
        private static PropertyInfo _abmxModifierLocationProperty;
        private static PropertyInfo _abmxModifierTransformProperty;
        private static FieldInfo _abmxModifierHasBaselineField;
        private static Type _hspePoseControllerType;
        private static Type _hspeCharaPoseControllerType;
        private static FieldInfo _hspeTargetField;
        private static FieldInfo _hspeBonesEditorField;
        private static FieldInfo _hspeDynamicBonesEditorField;
        private static FieldInfo _hspeBlendShapesEditorField;
        private static FieldInfo _hspeCollidersEditorField;
        private static FieldInfo _hspeIkEditorField;
        private static FieldInfo _hspeClothesTransformEditorField;
        private static FieldInfo _hspeBodyField;
        private static FieldInfo _hspeBoobsEditorField;
        private static FieldInfo _hspeSiriDamLField;
        private static FieldInfo _hspeSiriDamRField;
        private static FieldInfo _hspeKosiField;
        private static FieldInfo _hspeLeftFoot2Field;
        private static FieldInfo _hspeRightFoot2Field;
        private static FieldInfo _hspeTargetOciField;
        private static FieldInfo _hspeTargetOciCharField;
        private static FieldInfo _hspeTargetIsFemaleField;
        private static FieldInfo _hspeTargetFkObjectsField;
        private static MethodInfo _hspeRefreshFkBonesMethod;
        private static FieldInfo _hspeDynamicBonesBusyField;
        private static FieldInfo _hspeBlendShapesBusyField;

        private static FemaleForMaleStudioPlugin _instance;
        private static ConfigEntry<bool> _enabled;
        private static ConfigEntry<bool> _strictModBoneMapping;
        private static ConfigEntry<float> _boneGraphQuietTimeoutSeconds;
        private static ConfigEntry<float> _statusUiScale;
        private static ManualLogSource _log;

        private Harmony _harmony;
        private bool _busy;
        private bool _runtimeReady;
        private string _statusText = string.Empty;
        private float _statusUntil;
        private GUIStyle _statusStyle;
        private float _statusStyleScale = -1f;
        private Transaction _activeTransaction;
        private Transaction _snapshotTransaction;
        private bool _nodesConstraintsInstalled;
        private bool _timelineInstalled;
        private MethodInfo _nodesConstraintsExternalLoadMethod;
        private MethodInfo _nodesConstraintsClearAllMethod;
        private FieldInfo _nodesConstraintsSelfField;
        private MethodInfo _timelineSceneLoadMethod;
        private MethodInfo _timelineCoreSceneLoadMethod;
        private MethodInfo _timelineOnSceneSaveMethod;
        private MethodInfo _timelineGetSceneInfoMethod;
        private MethodInfo _timelineUpdateInterpolablesViewMethod;
        private MethodInfo _timelineCloseKeyframeWindowMethod;
        private PropertyInfo _timelineConfigAutoplayProperty;
        private FieldInfo _timelineSelfField;
        private FieldInfo _timelineInterpolablesField;
        private FieldInfo _timelineInterpolablesTreeField;
        private FieldInfo _timelineToDeleteField;
        private FieldInfo _timelineSelectedKeyframesField;
        private FieldInfo _timelineSelectedOciField;
        private MethodInfo _characterLoaderCloseMethod;

        private static bool Active
        {
            get
            {
                return _instance != null && _instance.enabled && _instance._runtimeReady &&
                       _enabled != null && _enabled.Value;
            }
        }

        private void Awake()
        {
            _instance = this;
            _log = Logger;
            _enabled = Config.Bind("常规", "启用Studio跨性别替换", true,
                "在女性角色列表中选择卡片后，可把工作区里选中的男角色完整重建为女性角色。");
            _strictModBoneMapping = Config.Bind("骨骼兼容", "扩展骨严格匹配", true,
                "骨骼只有在 ID、名称、骨组、骨权重及邻近四级父链都匹配时才迁移 FK 旋转。");
            _boneGraphQuietTimeoutSeconds = Config.Bind("骨骼兼容", "扩展骨无进展超时秒数", 45f,
                new ConfigDescription(
                    "女性对象重建后，等待 FK、IK、绑骨插件继续登记骨骼的无进展时限。每次检测到新骨骼或状态变化都会重新计时；总等待上限为该值的三倍。",
                    new AcceptableValueRange<float>(10f, 180f)));
            _statusUiScale = Config.Bind("界面", "加载提示额外缩放倍率", 1f,
                new ConfigDescription(
                    "加载提示会先按当前分辨率自动缩放；这里可再调整倍率。4K 默认自动放大为 2 倍。",
                    new AcceptableValueRange<float>(0.75f, 2f)));
            if (SexProperty == null || SexProperty.GetSetMethod(true) == null ||
                CharFileProperty == null || CharFileProperty.GetSetMethod(true) == null ||
                CharaListSexField == null || CharaFileSortField == null || ChangeButtonField == null ||
                GuideObjectDictionaryField == null)
            {
                Logger.LogError("R16 Studio 接口校验失败，插件未启用，避免损坏当前场景。");
                enabled = false;
                return;
            }

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(FemaleForMaleStudioPlugin).Assembly);
            if (!PatchNodesConstraintsCompatibility())
            {
                _harmony.UnpatchSelf();
                enabled = false;
                return;
            }

            if (!PatchTimelineCompatibility())
            {
                _harmony.UnpatchSelf();
                enabled = false;
                return;
            }

            if (!InitializeOptionalBoneReadinessAdapters())
            {
                _harmony.UnpatchSelf();
                enabled = false;
                return;
            }

            PatchCharacterLoaderCompatibility();

            _runtimeReady = true;
            Logger.LogInfo("Studio 跨性别完整替换已启用：人物卡预览替换入口、场景快照、对象 ID、FK/IK、绑骨与 Timeline 将在全场骨架稳定后迁移。");
        }

        private void OnDestroy()
        {
            _runtimeReady = false;
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
            }

            if (ReferenceEquals(_instance, this))
            {
                _instance = null;
            }
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(_statusText) || (!_busy && Time.unscaledTime > _statusUntil))
            {
                return;
            }

            float autoScale = Mathf.Clamp(Mathf.Min(Screen.width / 1920f, Screen.height / 1080f),
                1f, 2.5f);
            float userScale = _statusUiScale == null ? 1f : Mathf.Clamp(_statusUiScale.Value, 0.75f, 2f);
            float scale = autoScale * userScale;
            if (_statusStyle == null || !Mathf.Approximately(_statusStyleScale, scale))
            {
                _statusStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = Mathf.Max(16, Mathf.RoundToInt(20f * scale)),
                    fontStyle = FontStyle.Bold,
                    wordWrap = true,
                    padding = new RectOffset(
                        Mathf.RoundToInt(16f * scale), Mathf.RoundToInt(16f * scale),
                        Mathf.RoundToInt(8f * scale), Mathf.RoundToInt(8f * scale))
                };
                _statusStyleScale = scale;
            }

            float margin = 10f * scale;
            float width = Mathf.Min(880f * scale, Screen.width - margin * 2f);
            float contentWidth = Mathf.Max(80f, width - 32f * scale);
            float height = Mathf.Max(64f * scale,
                _statusStyle.CalcHeight(new GUIContent(_statusText), contentWidth) + 16f * scale);
            height = Mathf.Min(height, Screen.height * 0.25f);
            Rect rect = new Rect(Mathf.Max(margin, (Screen.width - width) * 0.5f),
                18f * scale, width, height);
            GUI.Box(rect, _statusText, _statusStyle);
        }

        private void SetStatus(string text, float seconds)
        {
            _statusText = text ?? string.Empty;
            _statusUntil = Time.unscaledTime + seconds;
        }

        private static List<OCIChar> GetSelectedCharacters()
        {
            List<OCIChar> result = new List<OCIChar>();
            if (!Singleton<StudioCore>.IsInstance())
            {
                return result;
            }

            int[] keys = Singleton<GuideObjectManager>.Instance.selectObjectKey;
            for (int i = 0; i < keys.Length; i++)
            {
                OCIChar character = StudioCore.GetCtrlInfo(keys[i]) as OCIChar;
                if (character != null && !result.Contains(character))
                {
                    result.Add(character);
                }
            }

            return result;
        }

        private static void RefreshFemaleChangeButton(CharaList list)
        {
            if (list == null || CharaListSexField == null || CharaFileSortField == null ||
                ChangeButtonField == null)
            {
                return;
            }

            int panelSex = (int)CharaListSexField.GetValue(list);
            if (panelSex != 1)
            {
                return;
            }

            CharaFileSort sort = CharaFileSortField.GetValue(list) as CharaFileSort;
            Button button = ChangeButtonField.GetValue(list) as Button;
            if (sort == null || button == null)
            {
                return;
            }

            bool hasCharacter = Active && !_instance._busy && sort.select != -1 &&
                                GetSelectedCharacters().Count > 0;
            button.interactable = hasCharacter;
        }

        private static string GetSelectedFemaleCard(CharaList list)
        {
            CharaFileSort sort = CharaFileSortField.GetValue(list) as CharaFileSort;
            return sort == null ? string.Empty : sort.selectPath;
        }

        private IEnumerator ReplaceSelectedMales(IList<int> requestedKeys, string femaleCardPath)
        {
            Exception unexpectedError = null;
            yield return DrainCoroutine(ReplaceSelectedMalesCore(requestedKeys, femaleCardPath),
                delegate(Exception error) { unexpectedError = error; });

            if (unexpectedError != null)
            {
                Transaction transaction = _activeTransaction;
                _log.LogError("替换事务发生未处理异常：\n" + unexpectedError);
                if (transaction != null && !transaction.LoadingRecovery &&
                    !string.IsNullOrEmpty(transaction.RecoveryPath) &&
                    File.Exists(transaction.RecoveryPath))
                {
                    Exception rollbackError = null;
                    yield return DrainCoroutine(
                        RollBack(transaction, "替换事务异常：" + unexpectedError.Message),
                        delegate(Exception error) { rollbackError = error; });
                    if (rollbackError != null)
                    {
                        HandleEmergencyTransactionFailure(transaction,
                            "异常后的自动回滚也失败：" + rollbackError.Message, rollbackError);
                    }
                }
                else
                {
                    HandleEmergencyTransactionFailure(transaction,
                        "替换事务无法继续安全恢复：" + unexpectedError.Message, unexpectedError);
                }
            }

            _activeTransaction = null;
            _snapshotTransaction = null;
            _busy = false;
        }

        private IEnumerator ReplaceSelectedMalesCore(IList<int> requestedKeys, string femaleCardPath)
        {
            _busy = true;
            SetStatus("正在验证女卡并保存转换前恢复快照……", 30f);

            Transaction transaction;
            Exception preparationError;
            if (!TryPrepareTransaction(requestedKeys, femaleCardPath, out transaction, out preparationError))
            {
                _busy = false;
                string message = "替换未开始：" + preparationError.Message;
                SetStatus(message, 10f);
                _log.LogError(message + "\n" + preparationError);
                yield break;
            }

            BeginConstraintLoadTracking(transaction, false);
            _activeTransaction = transaction;
            Exception clearError = null;
            try
            {
                ClearExtensionRuntime();
            }
            catch (Exception error)
            {
                clearError = UnwrapInvocationException(error);
            }

            if (clearError != null)
            {
                yield return RollBack(transaction, "隔离旧绑骨或 Timeline 轨道失败：" +
                                                   clearError.Message);
                yield break;
            }

            yield return null;
            SetStatus("正在以女性 OCI、女性身体和新 IK Solver 完整重载场景……", 60f);

            Exception loadError = null;
            IEnumerator loadRoutine = null;
            try
            {
                loadRoutine = Singleton<StudioCore>.Instance.LoadSceneCoroutine(transaction.ConvertedPath);
            }
            catch (Exception error)
            {
                loadError = error;
            }

            if (loadError == null)
            {
                yield return DrainCoroutine(loadRoutine,
                    delegate(Exception error) { loadError = error; });
            }

            if (loadError != null)
            {
                yield return RollBack(transaction, "女性场景载入失败：" + loadError.Message);
                yield break;
            }

            Exception boneReadyError = null;
            SetStatus("角色对象已创建，正在等待全场 FK、IK、ABMX 与身体骨架稳定……", 60f);
            yield return DrainCoroutine(WaitForSceneBoneGraphs(transaction,
                    delegate(Exception error) { boneReadyError = error; }),
                delegate(Exception error) { boneReadyError = error; });

            if (boneReadyError != null)
            {
                yield return RollBack(transaction, "全场骨架就绪检查失败：" + boneReadyError.Message);
                yield break;
            }

            string validationError = string.Empty;
            bool convertedValid = false;
            try
            {
                convertedValid = ValidateConvertedCharacters(transaction, out validationError);
            }
            catch (Exception error)
            {
                validationError = error.Message;
            }

            if (!convertedValid)
            {
                yield return RollBack(transaction, "女性角色重建校验失败：" + validationError);
                yield break;
            }

            Exception sanitizeError = null;
            BoneMigrationReport report = new BoneMigrationReport();
            try
            {
                report = SanitizeAndReapplyBones(transaction);
            }
            catch (Exception error)
            {
                sanitizeError = error;
            }

            if (sanitizeError != null)
            {
                yield return RollBack(transaction, "骨骼映射校验失败：" + sanitizeError.Message);
                yield break;
            }

            Exception refreshError = null;
            try
            {
                RefreshHsPeFkBones(transaction);
            }
            catch (Exception error)
            {
                refreshError = UnwrapInvocationException(error);
            }
            if (refreshError != null)
            {
                yield return RollBack(transaction,
                    "刷新 HS2PE 的当前 FK 对象表失败：" + refreshError.Message);
                yield break;
            }

            boneReadyError = null;
            SetStatus("FK 清理完成，正在复核全场对象索引、ABMX、HS2PE 与 GuideObject 注册表……", 60f);
            yield return DrainCoroutine(WaitForSceneBoneGraphs(transaction,
                    delegate(Exception error) { boneReadyError = error; }),
                delegate(Exception error) { boneReadyError = error; });
            if (boneReadyError != null)
            {
                yield return RollBack(transaction,
                    "FK 清理后的全场骨架就绪检查失败：" + boneReadyError.Message);
                yield break;
            }

            SetStatus("全场骨架已稳定，正在重建 NodesConstraints 与 Timeline 目标……", 60f);
            Exception rebindError = null;
            yield return DrainCoroutine(RebindExtensionsAfterSettle(transaction, true,
                    delegate(Exception error) { rebindError = error; }),
                delegate(Exception error) { rebindError = error; });
            if (rebindError != null)
            {
                yield return RollBack(transaction, "扩展插件安全重绑失败：" + rebindError.Message);
                yield break;
            }

            _activeTransaction = null;
            try
            {
                SelectFirstConvertedCharacter(transaction);
            }
            catch (Exception error)
            {
                _log.LogWarning("替换已完成，但无法自动选中角色：" + error.Message);
            }

            _busy = false;
            string success = string.Format(
                "已把 {0} 个男角色完整替换为女角色；FK 保留 {1}，重置 {2}，丢弃无目标节点 {3}，绑骨路径改写 {4}，Timeline 骨路径改写 {5}，重绑 {6} 条。",
                transaction.Keys.Count, report.Preserved, report.Reset, report.Pruned,
                transaction.ConstraintPathsRemapped, transaction.TimelinePathsRemapped,
                transaction.LoadedTimelineTrackCount);
            SetStatus(success, 9f);
            _log.LogInfo(success);
            _log.LogInfo("转换前恢复快照：" + transaction.RecoveryPath);
        }

        private void PatchCharacterLoaderCompatibility()
        {
            try
            {
                if (!IsPluginRunning("mikke.Charloader"))
                {
                    _log.LogInfo("Character Loader 未运行；仍可使用 Studio 原生女性角色列表的替换按钮。");
                    return;
                }

                Type characterLoaderType = AccessTools.TypeByName("CharLoader.CharLoaderStudio");
                if (characterLoaderType == null)
                {
                    _log.LogInfo("未检测到 Character Loader；仍可使用 Studio 原生女性角色列表的替换按钮。");
                    return;
                }

                MethodInfo target = AccessTools.Method(characterLoaderType, "ReplaceChara",
                    new[] { typeof(string), typeof(int) });
                MethodInfo prefix = AccessTools.Method(typeof(FemaleForMaleStudioPlugin),
                    nameof(CharacterLoaderReplacePrefix));
                if (target == null || prefix == null)
                {
                    throw new MissingMethodException(
                        "Character Loader 1.4.2 的 ReplaceChara(string, int) 接口不存在。");
                }

                _characterLoaderCloseMethod = AccessTools.Method(characterLoaderType,
                    "CloseWindowIfToggle", Type.EmptyTypes);
                _harmony.Patch(target,
                    prefix: new HarmonyMethod(prefix) { priority = Priority.First });
                _log.LogInfo("Character Loader 1.4.2 的卡片“替换”按钮已接入女替男完整重建流程。");
            }
            catch (Exception error)
            {
                _log.LogWarning("Character Loader 替换按钮接入失败；Studio 原生女性角色列表入口仍可使用。\n" +
                                error);
            }
        }

        private void CloseCharacterLoaderIfConfigured(object characterLoader)
        {
            if (characterLoader == null || _characterLoaderCloseMethod == null)
            {
                return;
            }

            try
            {
                _characterLoaderCloseMethod.Invoke(characterLoader, null);
            }
            catch (Exception error)
            {
                _log.LogWarning("无法按 Character Loader 配置自动关闭预览窗口，替换事务继续执行：" +
                                error.Message);
            }
        }

        private bool PatchNodesConstraintsCompatibility()
        {
            try
            {
                if (!IsPluginRunning("com.joan6694.illusionplugins.nodesconstraints"))
                {
                    _log.LogInfo("NodesConstraints 未运行；跳过可选绑骨路径兼容层。");
                    return true;
                }

                Type pluginType = AccessTools.TypeByName("NodesConstraints.NodesConstraints");
                if (pluginType == null)
                {
                    _log.LogInfo("未检测到 NodesConstraints；跳过可选绑骨路径兼容层。");
                    return true;
                }

                Type dictionaryList = typeof(List<KeyValuePair<int, ObjectCtrlInfo>>);
                _nodesConstraintsSelfField = AccessTools.Field(pluginType, "_self");
                _nodesConstraintsExternalLoadMethod = AccessTools.Method(pluginType,
                    "ExternalLoadScene", new[] { typeof(XmlNode) });
                _nodesConstraintsClearAllMethod = AccessTools.Method(pluginType,
                    "ClearAllConstraints", Type.EmptyTypes);
                MethodInfo outerTarget = AccessTools.Method(pluginType, "OnSceneLoad",
                    new[] { typeof(string), typeof(XmlNode) });
                MethodInfo outerPrefix = AccessTools.Method(typeof(FemaleForMaleStudioPlugin),
                    nameof(NodesConstraintsSceneLoadPrefix));
                MethodInfo target = AccessTools.Method(pluginType, "LoadSceneGeneric",
                    new[] { typeof(XmlNode), dictionaryList });
                MethodInfo prefix = AccessTools.Method(typeof(FemaleForMaleStudioPlugin),
                    nameof(NodesConstraintsLoadScenePrefix));
                MethodInfo postfix = AccessTools.Method(typeof(FemaleForMaleStudioPlugin),
                    nameof(NodesConstraintsLoadScenePostfix));
                MethodInfo finalizer = AccessTools.Method(typeof(FemaleForMaleStudioPlugin),
                    nameof(NodesConstraintsLoadSceneFinalizer));
                if (_nodesConstraintsSelfField == null || _nodesConstraintsExternalLoadMethod == null ||
                    _nodesConstraintsClearAllMethod == null || outerTarget == null || outerPrefix == null ||
                    target == null || prefix == null || postfix == null || finalizer == null)
                {
                    throw new MissingMethodException(
                        "NodesConstraints 1.6.2.1 的 ExternalLoadScene/LoadSceneGeneric 接口不存在。");
                }

                _harmony.Patch(outerTarget,
                    prefix: new HarmonyMethod(outerPrefix) { priority = Priority.First });
                _harmony.Patch(target,
                    prefix: new HarmonyMethod(prefix) { priority = Priority.First },
                    postfix: new HarmonyMethod(postfix) { priority = Priority.Last },
                    finalizer: new HarmonyMethod(finalizer) { priority = Priority.Last });

                MethodInfo saveTarget = AccessTools.Method(pluginType, "OnSceneSave",
                    new[] { typeof(string) });
                MethodInfo savePrefix = AccessTools.Method(typeof(FemaleForMaleStudioPlugin),
                    nameof(NodesConstraintsSavePrefix));
                MethodInfo savePostfix = AccessTools.Method(typeof(FemaleForMaleStudioPlugin),
                    nameof(NodesConstraintsSavePostfix));
                MethodInfo saveFinalizer = AccessTools.Method(typeof(FemaleForMaleStudioPlugin),
                    nameof(NodesConstraintsSaveFinalizer));
                if (saveTarget == null || savePrefix == null || savePostfix == null ||
                    saveFinalizer == null)
                {
                    throw new MissingMethodException("NodesConstraints 1.6.2.1 的 OnSceneSave 接口不存在。");
                }

                _harmony.Patch(saveTarget,
                    prefix: new HarmonyMethod(savePrefix) { priority = Priority.First },
                    postfix: new HarmonyMethod(savePostfix) { priority = Priority.Last },
                    finalizer: new HarmonyMethod(saveFinalizer) { priority = Priority.Last });
                _nodesConstraintsInstalled = true;
                _log.LogInfo("NodesConstraints 路径兼容层已启用。男女共用骨架路径原样保留，性别专属身体节点按实际目标映射。");
                return true;
            }
            catch (Exception error)
            {
                _log.LogError("NodesConstraints 兼容层安装失败；为防止绑骨静默丢失，跨性别替换已停用。\n" + error);
                return false;
            }
        }

        private bool PatchTimelineCompatibility()
        {
            try
            {
                if (!IsPluginRunning("com.joan6694.illusionplugins.timeline"))
                {
                    _log.LogInfo("Timeline 未运行；跳过可选动画轨道重绑层。");
                    return true;
                }

                Type timelineType = AccessTools.TypeByName("Timeline.Timeline");
                if (timelineType == null)
                {
                    _log.LogInfo("未检测到 Timeline；跳过可选动画轨道重绑层。");
                    return true;
                }

                Type dictionaryList = typeof(List<KeyValuePair<int, ObjectCtrlInfo>>);
                _timelineSceneLoadMethod = AccessTools.Method(timelineType, "SceneLoad",
                    new[] { typeof(string), typeof(XmlNode) });
                _timelineCoreSceneLoadMethod = AccessTools.Method(timelineType, "SceneLoad",
                    new[] { typeof(XmlNode), dictionaryList });
                _timelineOnSceneSaveMethod = AccessTools.Method(timelineType, "OnSceneSave",
                    new[] { typeof(string) });
                _timelineGetSceneInfoMethod = AccessTools.Method(timelineType, "GetSceneInfo",
                    Type.EmptyTypes);
                _timelineUpdateInterpolablesViewMethod = AccessTools.Method(timelineType,
                    "UpdateInterpolablesView", Type.EmptyTypes);
                _timelineCloseKeyframeWindowMethod = AccessTools.Method(timelineType,
                    "CloseKeyframeWindow", Type.EmptyTypes);
                _timelineConfigAutoplayProperty = AccessTools.Property(timelineType,
                    "ConfigAutoplay");
                _timelineSelfField = AccessTools.Field(timelineType, "_self");
                _timelineInterpolablesField = AccessTools.Field(timelineType, "_interpolables");
                _timelineInterpolablesTreeField = AccessTools.Field(timelineType, "_interpolablesTree");
                _timelineToDeleteField = AccessTools.Field(timelineType, "_toDelete");
                _timelineSelectedKeyframesField = AccessTools.Field(timelineType, "_selectedKeyframes");
                _timelineSelectedOciField = AccessTools.Field(timelineType, "_selectedOCI");
                MethodInfo outerPrefix = AccessTools.Method(typeof(FemaleForMaleStudioPlugin),
                    nameof(TimelineSceneLoadPrefix));
                MethodInfo corePostfix = AccessTools.Method(typeof(FemaleForMaleStudioPlugin),
                    nameof(TimelineCoreSceneLoadPostfix));
                MethodInfo coreFinalizer = AccessTools.Method(typeof(FemaleForMaleStudioPlugin),
                    nameof(TimelineCoreSceneLoadFinalizer));
                MethodInfo savePrefix = AccessTools.Method(typeof(FemaleForMaleStudioPlugin),
                    nameof(TimelineSavePrefix));
                MethodInfo savePostfix = AccessTools.Method(typeof(FemaleForMaleStudioPlugin),
                    nameof(TimelineSavePostfix));
                MethodInfo saveFinalizer = AccessTools.Method(typeof(FemaleForMaleStudioPlugin),
                    nameof(TimelineSaveFinalizer));
                if (_timelineSceneLoadMethod == null || _timelineCoreSceneLoadMethod == null ||
                    _timelineOnSceneSaveMethod == null || _timelineGetSceneInfoMethod == null ||
                    _timelineUpdateInterpolablesViewMethod == null ||
                    _timelineCloseKeyframeWindowMethod == null ||
                    _timelineConfigAutoplayProperty == null ||
                    _timelineSelfField == null ||
                    _timelineInterpolablesField == null || _timelineInterpolablesTreeField == null ||
                    _timelineToDeleteField == null || _timelineSelectedKeyframesField == null ||
                    _timelineSelectedOciField == null || outerPrefix == null || corePostfix == null ||
                    coreFinalizer == null || savePrefix == null || savePostfix == null ||
                    saveFinalizer == null)
                {
                    throw new MissingMemberException(
                        "Timeline 1.5.5.1 的场景载入、保存或轨道集合接口不完整。");
                }

                _harmony.Patch(_timelineSceneLoadMethod,
                    prefix: new HarmonyMethod(outerPrefix) { priority = Priority.First });
                _harmony.Patch(_timelineCoreSceneLoadMethod,
                    postfix: new HarmonyMethod(corePostfix) { priority = Priority.Last },
                    finalizer: new HarmonyMethod(coreFinalizer) { priority = Priority.Last });
                _harmony.Patch(_timelineOnSceneSaveMethod,
                    prefix: new HarmonyMethod(savePrefix) { priority = Priority.First },
                    postfix: new HarmonyMethod(savePostfix) { priority = Priority.Last },
                    finalizer: new HarmonyMethod(saveFinalizer) { priority = Priority.Last });
                _timelineInstalled = true;
                _log.LogInfo("Timeline 安全重绑层已启用。不会控制播放/暂停状态；全场骨架稳定后才重建轨道目标。");
                return true;
            }
            catch (Exception error)
            {
                _log.LogError("Timeline 兼容层安装失败；为防止播放时骨骼错绑，跨性别替换已停用。\n" + error);
                return false;
            }
        }

        private bool InitializeOptionalBoneReadinessAdapters()
        {
            try
            {
                bool abmxRunning = IsPluginRunning("KKABMX.Core");
                _abmxBoneControllerType = abmxRunning
                    ? AccessTools.TypeByName("KKABMX.Core.BoneController")
                    : null;
                if (abmxRunning && _abmxBoneControllerType == null)
                {
                    throw new MissingMemberException("HS2ABMX 正在运行，但 BoneController 类型不可读取。");
                }

                if (_abmxBoneControllerType != null)
                {
                    _abmxBoneSearcherProperty = AccessTools.Property(_abmxBoneControllerType,
                        "BoneSearcher");
                    _abmxNeedsFullRefreshProperty = AccessTools.Property(_abmxBoneControllerType,
                        "NeedsFullRefresh");
                    _abmxNeedsBaselineUpdateProperty = AccessTools.Property(_abmxBoneControllerType,
                        "NeedsBaselineUpdate");
                    _abmxBaselineKnownField = FindFieldInHierarchy(_abmxBoneControllerType,
                        "_baselineKnown");
                    _abmxPreviousAnimSpeedField = FindFieldInHierarchy(_abmxBoneControllerType,
                        "_previousAnimSpeed");
                    _abmxPartialBaselineTargetsField = FindFieldInHierarchy(_abmxBoneControllerType,
                        "_partialBaselineUpdateTargets");
                    _abmxGetAllModifiersMethod = AccessTools.Method(_abmxBoneControllerType,
                        "GetAllModifiers", Type.EmptyTypes);
                    Type boneFinderType = _abmxBoneSearcherProperty == null
                        ? null
                        : _abmxBoneSearcherProperty.PropertyType;
                    _abmxBoneSearcherControlField = boneFinderType == null
                        ? null
                        : FindFieldInHierarchy(boneFinderType, "_ctrl");
                    Type modifierType = AccessTools.TypeByName("KKABMX.Core.BoneModifier");
                    _abmxModifierNameProperty = modifierType == null
                        ? null
                        : AccessTools.Property(modifierType, "BoneName");
                    _abmxModifierLocationProperty = modifierType == null
                        ? null
                        : AccessTools.Property(modifierType, "BoneLocation");
                    _abmxModifierTransformProperty = modifierType == null
                        ? null
                        : AccessTools.Property(modifierType, "BoneTransform");
                    _abmxModifierHasBaselineField = modifierType == null
                        ? null
                        : FindFieldInHierarchy(modifierType, "_hasBaseline");
                    if (_abmxBoneSearcherProperty == null ||
                        _abmxNeedsFullRefreshProperty == null ||
                        _abmxNeedsBaselineUpdateProperty == null ||
                        _abmxBoneSearcherControlField == null ||
                        _abmxBaselineKnownField == null || _abmxPreviousAnimSpeedField == null ||
                        _abmxPartialBaselineTargetsField == null || _abmxGetAllModifiersMethod == null ||
                        _abmxModifierNameProperty == null || _abmxModifierLocationProperty == null ||
                        _abmxModifierTransformProperty == null || _abmxModifierHasBaselineField == null)
                    {
                        throw new MissingMemberException("HS2ABMX 5.2.2 的稳定状态接口不完整。");
                    }

                    _log.LogInfo("HS2ABMX 全场 baseline 稳定检测已启用。");
                }

                bool hsPeRunning = IsPluginRunning("com.joan6694.illusionplugins.poseeditor");
                _hspePoseControllerType = hsPeRunning
                    ? AccessTools.TypeByName("HSPE.PoseController")
                    : null;
                _hspeCharaPoseControllerType = hsPeRunning
                    ? AccessTools.TypeByName("HSPE.CharaPoseController")
                    : null;
                if (hsPeRunning &&
                    (_hspePoseControllerType == null || _hspeCharaPoseControllerType == null))
                {
                    throw new MissingMemberException(
                        "HS2PE 正在运行，但基础或角色 PoseController 类型不可读取。");
                }

                if (_hspePoseControllerType != null || _hspeCharaPoseControllerType != null)
                {
                    if (_hspePoseControllerType == null || _hspeCharaPoseControllerType == null)
                    {
                        throw new MissingMemberException("HS2PE 的基础或角色 PoseController 类型不完整。");
                    }

                    _hspeTargetField = FindFieldInHierarchy(_hspeCharaPoseControllerType, "_target");
                    _hspeBonesEditorField = FindFieldInHierarchy(_hspeCharaPoseControllerType,
                        "_bonesEditor");
                    _hspeDynamicBonesEditorField = FindFieldInHierarchy(_hspeCharaPoseControllerType,
                        "_dynamicBonesEditor");
                    _hspeBlendShapesEditorField = FindFieldInHierarchy(_hspeCharaPoseControllerType,
                        "_blendShapesEditor");
                    _hspeCollidersEditorField = FindFieldInHierarchy(_hspeCharaPoseControllerType,
                        "_collidersEditor");
                    _hspeIkEditorField = FindFieldInHierarchy(_hspeCharaPoseControllerType,
                        "_ikEditor");
                    _hspeClothesTransformEditorField = FindFieldInHierarchy(
                        _hspeCharaPoseControllerType, "_clothesTransformEditor");
                    _hspeBodyField = FindFieldInHierarchy(_hspeCharaPoseControllerType, "_body");
                    _hspeBoobsEditorField = FindFieldInHierarchy(_hspeCharaPoseControllerType,
                        "_boobsEditor");
                    _hspeSiriDamLField = FindFieldInHierarchy(_hspeCharaPoseControllerType,
                        "_siriDamL");
                    _hspeSiriDamRField = FindFieldInHierarchy(_hspeCharaPoseControllerType,
                        "_siriDamR");
                    _hspeKosiField = FindFieldInHierarchy(_hspeCharaPoseControllerType, "_kosi");
                    _hspeLeftFoot2Field = FindFieldInHierarchy(_hspeCharaPoseControllerType,
                        "_leftFoot2");
                    _hspeRightFoot2Field = FindFieldInHierarchy(_hspeCharaPoseControllerType,
                        "_rightFoot2");
                    if (_hspeTargetField == null || _hspeBonesEditorField == null ||
                        _hspeDynamicBonesEditorField == null || _hspeBlendShapesEditorField == null ||
                        _hspeCollidersEditorField == null || _hspeIkEditorField == null ||
                        _hspeClothesTransformEditorField == null || _hspeBodyField == null ||
                        _hspeBoobsEditorField == null || _hspeSiriDamLField == null ||
                        _hspeSiriDamRField == null || _hspeKosiField == null ||
                        _hspeLeftFoot2Field == null || _hspeRightFoot2Field == null)
                    {
                        throw new MissingMemberException("HS2PE 2.21.4 的角色骨架状态接口不完整。");
                    }

                    Type targetType = _hspeTargetField.FieldType;
                    _hspeTargetOciField = AccessTools.Field(targetType, "oci");
                    _hspeTargetOciCharField = AccessTools.Field(targetType, "ociChar");
                    _hspeTargetIsFemaleField = AccessTools.Field(targetType, "isFemale");
                    _hspeTargetFkObjectsField = AccessTools.Field(targetType, "fkObjects");
                    _hspeRefreshFkBonesMethod = AccessTools.Method(targetType, "RefreshFKBones",
                        Type.EmptyTypes);
                    _hspeDynamicBonesBusyField = FindFieldInHierarchy(
                        _hspeDynamicBonesEditorField.FieldType, "_isBusy");
                    _hspeBlendShapesBusyField = FindFieldInHierarchy(
                        _hspeBlendShapesEditorField.FieldType, "_isBusy");
                    if (_hspeTargetOciField == null || _hspeTargetOciCharField == null ||
                        _hspeTargetIsFemaleField == null || _hspeTargetFkObjectsField == null ||
                        _hspeRefreshFkBonesMethod == null || _hspeDynamicBonesBusyField == null ||
                        _hspeBlendShapesBusyField == null)
                    {
                        throw new MissingMemberException("HS2PE 2.21.4 的目标或模块状态接口不完整。");
                    }

                    _log.LogInfo("HS2PE 全场骨骼编辑器稳定检测已启用。");
                }

                return true;
            }
            catch (Exception error)
            {
                _log.LogError("扩展骨骼插件接口校验失败；跨性别替换已停用，避免使用未完成的 ABMX/HSPE 状态。\n" +
                              error);
                return false;
            }
        }

        private static bool IsPluginRunning(string guid)
        {
            PluginInfo info;
            return Chainloader.PluginInfos.TryGetValue(guid, out info) && info != null &&
                   info.Instance != null && info.Instance.enabled;
        }

        private static bool NodesConstraintsSceneLoadPrefix(object __instance, string __0, XmlNode __1)
        {
            if (_instance == null || _instance._activeTransaction == null)
            {
                return true;
            }

            Transaction transaction = _instance._activeTransaction;
            try
            {
                XmlNode constraintsRoot = __1 == null ? null : __1.FirstChild;
                if (__instance == null || constraintsRoot == null)
                {
                    throw new InvalidDataException("NodesConstraints 场景数据为空，无法延迟重绑。");
                }

                transaction.ConstraintSceneLoadDeferred = true;
                transaction.DeferredNodesConstraintsInstance = __instance;
                transaction.DeferredNodesConstraintsPath = __0 ?? string.Empty;
                transaction.DeferredNodesConstraintsNode = constraintsRoot.CloneNode(true);
            }
            catch (Exception error)
            {
                transaction.ConstraintAdapterError = UnwrapInvocationException(error).Message;
                _log.LogError("捕获 NodesConstraints 场景数据失败：" + error);
            }

            return false;
        }

        private static bool TimelineSceneLoadPrefix(object __instance, string __0, XmlNode __1)
        {
            if (_instance == null || _instance._activeTransaction == null)
            {
                return true;
            }

            Transaction transaction = _instance._activeTransaction;
            try
            {
                if (__instance == null || __1 == null)
                {
                    throw new InvalidDataException("Timeline 场景数据为空，无法延迟重绑。");
                }

                transaction.DeferredTimelineInstance = __instance;
                transaction.DeferredTimelinePath = __0 ?? string.Empty;
                transaction.DeferredTimelineNode = __1.CloneNode(true);
                transaction.TimelineLoadObserved = true;
                transaction.TimelineSceneLoadDeferred = true;
                transaction.ExpectedTimelineTrackCount = CountTimelineTracks(__1);
            }
            catch (Exception error)
            {
                transaction.TimelineAdapterError = UnwrapInvocationException(error).Message;
                _log.LogError("捕获 Timeline 场景轨道数据失败：" + error);
            }

            return false;
        }

        private static void TimelineCoreSceneLoadPostfix(object __instance)
        {
            if (_instance == null || _instance._activeTransaction == null)
            {
                return;
            }

            Transaction transaction = _instance._activeTransaction;
            try
            {
                transaction.DeferredTimelineInstance = __instance ?? transaction.DeferredTimelineInstance;
                bool ready;
                transaction.TimelineBindingFingerprint = GetSceneBoneGraphFingerprint(transaction, out ready);
                int loadedCount;
                int invalidCount;
                string detail;
                AuditTimelineBindings(__instance, transaction, out loadedCount, out invalidCount, out detail);
                transaction.LoadedTimelineTrackCount = loadedCount;
                transaction.InvalidTimelineBindingCount = invalidCount;
                if (!ready || transaction.TimelineBindingFingerprint != transaction.SettledGraphFingerprint)
                {
                    transaction.TimelineAdapterError = "Timeline 重绑时全场骨架图又发生变化。";
                }
                else if (invalidCount > 0 ||
                         loadedCount != transaction.ExpectedTimelineTrackCount)
                {
                    transaction.TimelineAdapterError = string.Format(
                        "Timeline 轨道完整性检查失败：快照 {0} 条、载入 {1} 条、无效 {2} 条。{3}",
                        transaction.ExpectedTimelineTrackCount, loadedCount, invalidCount, detail);
                }

                if (!string.IsNullOrEmpty(transaction.TimelineAdapterError))
                {
                    _instance.ClearTimelineRuntime();
                }
            }
            catch (Exception error)
            {
                transaction.TimelineAdapterError = UnwrapInvocationException(error).Message;
                _instance.ClearTimelineRuntimeBestEffort();
            }
            finally
            {
                transaction.TimelineLoadCompleted = true;
            }
        }

        private static Exception TimelineCoreSceneLoadFinalizer(Exception __exception)
        {
            if (__exception != null && _instance != null && _instance._activeTransaction != null)
            {
                _instance._activeTransaction.TimelineAdapterError =
                    UnwrapInvocationException(__exception).Message;
                _instance._activeTransaction.TimelineLoadCompleted = true;
                _instance.ClearTimelineRuntimeBestEffort();
                _log.LogError("Timeline 原始轨道载入方法发生异常：" + __exception);
                return null;
            }

            return __exception;
        }

        private static void TimelineSavePrefix(object __instance)
        {
            if (_instance == null || _instance._snapshotTransaction == null)
            {
                return;
            }

            Transaction transaction = _instance._snapshotTransaction;
            transaction.TimelineSaveObserved = true;
            transaction.TimelineSaveCompleted = false;
            transaction.TimelineSaveError = string.Empty;
            try
            {
                if (__instance == null)
                {
                    throw new InvalidOperationException("未取得 Timeline 运行实例。");
                }

                transaction.TimelineRuntimeTrackCount = CountSavableTimelineTracks(__instance);
            }
            catch (Exception error)
            {
                transaction.TimelineSaveError = UnwrapInvocationException(error).Message;
            }
        }

        private static void TimelineSavePostfix()
        {
            if (_instance == null || _instance._snapshotTransaction == null)
            {
                return;
            }

            Transaction transaction = _instance._snapshotTransaction;
            try
            {
                XmlNode savedRoot = _instance._timelineGetSceneInfoMethod.Invoke(null, null) as XmlNode;
                transaction.TimelineSnapshotTrackCount = CountTimelineTracks(savedRoot);
                if (transaction.TimelineSnapshotTrackCount != transaction.TimelineRuntimeTrackCount)
                {
                    throw new InvalidDataException(string.Format(
                        "Timeline 运行时应保存 {0} 条轨道，快照实际写入 {1} 条。",
                        transaction.TimelineRuntimeTrackCount,
                        transaction.TimelineSnapshotTrackCount));
                }
            }
            catch (Exception error)
            {
                transaction.TimelineSaveError = UnwrapInvocationException(error).Message;
            }
            finally
            {
                transaction.TimelineSaveCompleted = true;
            }
        }

        private static Exception TimelineSaveFinalizer(Exception __exception)
        {
            if (__exception != null && _instance != null && _instance._snapshotTransaction != null)
            {
                Transaction transaction = _instance._snapshotTransaction;
                transaction.TimelineSaveError = UnwrapInvocationException(__exception).Message;
                transaction.TimelineSaveCompleted = true;
                _log.LogError("Timeline 场景保存回调发生异常：" + __exception);
            }

            return __exception;
        }

        private static void NodesConstraintsLoadScenePrefix(object __instance, XmlNode __0,
            List<KeyValuePair<int, ObjectCtrlInfo>> __1)
        {
            if (_instance == null || _instance._activeTransaction == null)
            {
                return;
            }

            try
            {
                Transaction transaction = _instance._activeTransaction;
                transaction.ConstraintLoadObserved = true;
                ValidateConstraintDocumentCount(__0, transaction);
                if (!transaction.LoadingRecovery)
                {
                    _instance.RewriteNodesConstraintPaths(__0, __1, transaction);
                }

                transaction.DeferredNodesConstraintsInstance = __instance;
            }
            catch (Exception error)
            {
                _instance._activeTransaction.ConstraintAdapterError = error.Message;
                _log.LogError("NodesConstraints 路径改写失败：" + error);
            }
        }

        private static void NodesConstraintsSavePrefix(object __instance)
        {
            if (_instance == null || _instance._snapshotTransaction == null)
            {
                return;
            }

            Transaction transaction = _instance._snapshotTransaction;
            transaction.ConstraintSaveObserved = true;
            try
            {
                FieldInfo constraintsField = __instance == null
                    ? null
                    : AccessTools.Field(__instance.GetType(), "_constraints");
                System.Collections.IList constraints = constraintsField == null
                    ? null
                    : constraintsField.GetValue(__instance) as System.Collections.IList;
                if (constraints == null)
                {
                    throw new MissingFieldException("NodesConstraints._constraints 不可读取。");
                }

                transaction.SavedConstraintCount = constraints.Count;
            }
            catch (Exception error)
            {
                transaction.ConstraintSaveError = error.Message;
            }
        }

        private static void NodesConstraintsSavePostfix()
        {
            if (_instance != null && _instance._snapshotTransaction != null)
            {
                _instance._snapshotTransaction.ConstraintSaveCompleted = true;
            }
        }

        private static Exception NodesConstraintsSaveFinalizer(Exception __exception)
        {
            if (__exception != null && _instance != null && _instance._snapshotTransaction != null)
            {
                _instance._snapshotTransaction.ConstraintSaveError = __exception.Message;
                _log.LogError("NodesConstraints 场景保存回调发生异常：" + __exception);
            }

            return __exception;
        }

        private static void NodesConstraintsLoadScenePostfix(object __instance)
        {
            if (_instance == null || _instance._activeTransaction == null || __instance == null)
            {
                return;
            }

            try
            {
                _instance._activeTransaction.ConstraintLoadCompleted = true;
                FieldInfo constraintsField = AccessTools.Field(__instance.GetType(), "_constraints");
                System.Collections.IList constraints = constraintsField == null
                    ? null
                    : constraintsField.GetValue(__instance) as System.Collections.IList;
                if (constraints == null)
                {
                    throw new MissingFieldException("NodesConstraints._constraints 不可读取。");
                }

                _instance._activeTransaction.NodesConstraintsInstance = __instance;
                if (_instance._activeTransaction.LoadingRecovery)
                {
                    return;
                }

                HashSet<int> loadedIds = new HashSet<int>();
                for (int i = 0; i < constraints.Count; i++)
                {
                    object constraint = constraints[i];
                    FieldInfo idField = constraint == null
                        ? null
                        : AccessTools.Field(constraint.GetType(), "uniqueLoadId");
                    object value = idField == null ? null : idField.GetValue(constraint);
                    if (value is int)
                    {
                        int loadedId = (int)value;
                        loadedIds.Add(loadedId);
                        if (_instance._activeTransaction.ExpectedConstraintIds.Contains(loadedId))
                        {
                            _instance._activeTransaction.LoadedConstraintObjects[loadedId] = constraint;
                        }
                    }
                }

                foreach (int expectedId in _instance._activeTransaction.ExpectedConstraintIds)
                {
                    if (!loadedIds.Contains(expectedId))
                    {
                        _instance._activeTransaction.FailedConstraintIds.Add(expectedId);
                    }
                }
            }
            catch (Exception error)
            {
                _instance._activeTransaction.ConstraintAdapterError = error.Message;
                _log.LogError("NodesConstraints 载入结果校验失败：" + error);
            }
        }

        private static void BeginConstraintLoadTracking(Transaction transaction, bool loadingRecovery)
        {
            transaction.LoadingRecovery = loadingRecovery;
            ResetNodesConstraintReplayState(transaction);
            transaction.ConstraintSceneLoadDeferred = false;
            transaction.DeferredNodesConstraintsInstance = null;
            transaction.DeferredNodesConstraintsPath = string.Empty;
            transaction.DeferredNodesConstraintsNode = null;
            transaction.TimelineSceneLoadDeferred = false;
            ResetTimelineReplayState(transaction);
            transaction.TimelineLoadObserved = false;
            transaction.DeferredTimelineInstance = null;
            transaction.DeferredTimelinePath = string.Empty;
            transaction.DeferredTimelineNode = null;
            transaction.ExpectedTimelineTrackCount = -1;
            transaction.TimelinePathsRemapped = 0;
            transaction.SettledGraphFingerprint = int.MinValue;
            transaction.BoneGraphPermanentError = string.Empty;
            transaction.SceneLoadStartFrame = Time.frameCount;
        }

        private static void ResetNodesConstraintReplayState(Transaction transaction)
        {
            transaction.ConstraintLoadObserved = false;
            transaction.ConstraintLoadCompleted = false;
            transaction.ConstraintAdapterError = string.Empty;
            transaction.ExpectedConstraintIds.Clear();
            transaction.FailedConstraintIds.Clear();
            transaction.LoadedConstraintObjects.Clear();
            transaction.NodesConstraintsInstance = null;
            transaction.UnidentifiedConstraints = 0;
            transaction.ConstraintPathsRemapped = 0;
        }

        private static void ResetTimelineReplayState(Transaction transaction)
        {
            transaction.TimelineLoadCompleted = false;
            transaction.TimelineAdapterError = string.Empty;
            transaction.TimelineBindingFingerprint = int.MinValue;
            transaction.LoadedTimelineTrackCount = 0;
            transaction.InvalidTimelineBindingCount = 0;
        }

        private void ClearExtensionRuntime()
        {
            if (_timelineInstalled)
            {
                ClearTimelineRuntime();
            }

            if (_nodesConstraintsInstalled)
            {
                object nodes = _nodesConstraintsSelfField.GetValue(null);
                if (nodes == null)
                {
                    throw new InvalidOperationException("未取得 NodesConstraints 运行实例。");
                }

                _nodesConstraintsClearAllMethod.Invoke(nodes, null);
            }
        }

        private Exception ClearExtensionRuntimeForRollback()
        {
            List<string> failures = new List<string>();
            if (_timelineInstalled)
            {
                try
                {
                    ClearTimelineRuntime();
                    object timeline = _timelineSelfField.GetValue(null);
                    RequireEmptyCollection(_timelineInterpolablesField.GetValue(timeline),
                        "Timeline._interpolables");
                    RequireTimelineTreeEmpty(_timelineInterpolablesTreeField.GetValue(timeline));
                    RequireEmptyCollection(_timelineToDeleteField.GetValue(timeline),
                        "Timeline._toDelete");
                    RequireEmptyCollection(_timelineSelectedKeyframesField.GetValue(timeline),
                        "Timeline._selectedKeyframes");
                    if (_timelineSelectedOciField.GetValue(timeline) != null)
                    {
                        throw new InvalidOperationException("Timeline._selectedOCI 未清空。");
                    }
                }
                catch (Exception error)
                {
                    failures.Add("Timeline：" + UnwrapInvocationException(error).Message);
                }
            }

            // Always attempt NodesConstraints even when Timeline cleanup failed. Otherwise its
            // Update loop can keep ticking bindings whose source objects are about to be destroyed.
            if (_nodesConstraintsInstalled)
            {
                try
                {
                    object nodes = _nodesConstraintsSelfField.GetValue(null);
                    if (nodes == null)
                    {
                        throw new InvalidOperationException("未取得 NodesConstraints 运行实例。");
                    }

                    _nodesConstraintsClearAllMethod.Invoke(nodes, null);
                    FieldInfo constraintsField = AccessTools.Field(nodes.GetType(), "_constraints");
                    if (constraintsField == null)
                    {
                        throw new MissingFieldException("NodesConstraints._constraints 不可读取。");
                    }

                    RequireEmptyCollection(constraintsField.GetValue(nodes),
                        "NodesConstraints._constraints");
                }
                catch (Exception error)
                {
                    failures.Add("NodesConstraints：" +
                                 UnwrapInvocationException(error).Message);
                }
            }

            return failures.Count == 0
                ? null
                : new InvalidOperationException(
                    "回滚前无法确认扩展运行状态已清空；为避免残留绑定访问即将销毁的对象，已停止自动载入恢复快照。" +
                    string.Join("；", failures.ToArray()));
        }

        private void ClearTimelineRuntime()
        {
            object timeline = _timelineSelfField.GetValue(null);
            if (timeline == null)
            {
                throw new InvalidOperationException("未取得 Timeline 运行实例。");
            }

            ClearCollection(_timelineInterpolablesField.GetValue(timeline), "Timeline._interpolables");
            ClearCollection(_timelineInterpolablesTreeField.GetValue(timeline),
                "Timeline._interpolablesTree");
            ClearCollection(_timelineToDeleteField.GetValue(timeline), "Timeline._toDelete");
            ClearCollection(_timelineSelectedKeyframesField.GetValue(timeline),
                "Timeline._selectedKeyframes");
            _timelineSelectedOciField.SetValue(timeline, null);
        }

        private void ClearTimelineRuntimeBestEffort()
        {
            try
            {
                if (_timelineInstalled)
                {
                    ClearTimelineRuntime();
                }
            }
            catch (Exception error)
            {
                _log.LogError("清理 Timeline 旧轨道失败：" + UnwrapInvocationException(error));
            }
        }

        private void ClearNodesConstraintsRuntimeBestEffort()
        {
            try
            {
                if (_nodesConstraintsInstalled)
                {
                    object nodes = _nodesConstraintsSelfField.GetValue(null);
                    if (nodes != null)
                    {
                        _nodesConstraintsClearAllMethod.Invoke(nodes, null);
                    }
                }
            }
            catch (Exception error)
            {
                _log.LogError("清理 NodesConstraints 旧约束失败：" +
                              UnwrapInvocationException(error));
            }
        }

        private static void ClearCollection(object collection, string name)
        {
            if (collection == null)
            {
                throw new InvalidOperationException(name + " 不可读取。");
            }

            IDictionary dictionary = collection as IDictionary;
            if (dictionary != null)
            {
                dictionary.Clear();
                return;
            }

            IList list = collection as IList;
            if (list != null)
            {
                list.Clear();
                return;
            }

            MethodInfo clear = AccessTools.Method(collection.GetType(), "Clear", Type.EmptyTypes);
            if (clear == null)
            {
                throw new MissingMethodException(name + " 没有 Clear 方法。");
            }

            clear.Invoke(collection, null);
        }

        private static void RequireEmptyCollection(object collection, string name)
        {
            int count = GetCollectionCount(collection);
            if (count != 0)
            {
                throw new InvalidOperationException(string.Format(
                    "{0} 清理后仍有 {1} 项。", name, count));
            }
        }

        private static void RequireTimelineTreeEmpty(object tree)
        {
            if (tree == null)
            {
                throw new InvalidOperationException("Timeline._interpolablesTree 不可读取。");
            }

            // Timeline.Tree does not implement ICollection/IEnumerable. Its public tree list and
            // private node index must both be empty after Clear(), otherwise a stale leaf may
            // survive even though the other view looks empty.
            PropertyInfo treeProperty = AccessTools.Property(tree.GetType(), "tree");
            FieldInfo nodesField = FindFieldInHierarchy(tree.GetType(), "_nodes");
            if (treeProperty == null || nodesField == null)
            {
                throw new MissingMemberException(
                    "Timeline._interpolablesTree 的 tree/_nodes 接口不可读取。");
            }

            RequireEmptyCollection(treeProperty.GetValue(tree, null),
                "Timeline._interpolablesTree.tree");
            RequireEmptyCollection(nodesField.GetValue(tree),
                "Timeline._interpolablesTree._nodes");
        }

        private Exception ReplayNodesConstraints(Transaction transaction, bool auditConvertedEndpoints)
        {
            if (!_nodesConstraintsInstalled)
            {
                return null;
            }

            if (!transaction.ConstraintSceneLoadDeferred ||
                transaction.DeferredNodesConstraintsNode == null)
            {
                return new InvalidDataException("没有捕获到 NodesConstraints 场景数据。");
            }

            object nodes = transaction.DeferredNodesConstraintsInstance ??
                           _nodesConstraintsSelfField.GetValue(null);
            if (nodes == null)
            {
                return new InvalidOperationException("未取得 NodesConstraints 运行实例。");
            }

            ResetNodesConstraintReplayState(transaction);
            try
            {
                BuildStableObjectList(transaction);
                XmlNode replayNode = transaction.DeferredNodesConstraintsNode.CloneNode(true);
                _nodesConstraintsExternalLoadMethod.Invoke(nodes, new object[] { replayNode });
            }
            catch (Exception error)
            {
                transaction.ConstraintAdapterError = UnwrapInvocationException(error).Message;
            }

            AuditNodesConstraintsAfterSettle(transaction, auditConvertedEndpoints);
            if (!string.IsNullOrEmpty(transaction.ConstraintAdapterError))
            {
                return new InvalidDataException(transaction.ConstraintAdapterError);
            }

            if (auditConvertedEndpoints && transaction.UnresolvedConstraints > 0)
            {
                return new InvalidDataException(string.Format(
                    "有 {0} 条绑骨约束无法安全映射。", transaction.UnresolvedConstraints));
            }

            return null;
        }

        private static List<KeyValuePair<int, ObjectCtrlInfo>> BuildStableObjectList(
            Transaction transaction)
        {
            if (!Singleton<StudioCore>.IsInstance())
            {
                throw new InvalidOperationException("Studio 尚未初始化。");
            }

            Dictionary<int, ObjectCtrlInfo> current = Singleton<StudioCore>.Instance.dicObjectCtrl;
            if (current.Count != transaction.ExpectedObjectKeys.Count)
            {
                throw new InvalidDataException(string.Format(
                    "全场对象表数量不一致：快照 {0}，当前 {1}。",
                    transaction.ExpectedObjectKeys.Count, current.Count));
            }

            List<KeyValuePair<int, ObjectCtrlInfo>> result =
                new List<KeyValuePair<int, ObjectCtrlInfo>>(transaction.ExpectedObjectKeys.Count);
            for (int i = 0; i < transaction.ExpectedObjectKeys.Count; i++)
            {
                int key = transaction.ExpectedObjectKeys[i];
                ObjectCtrlInfo value;
                if (!current.TryGetValue(key, out value) || value == null)
                {
                    throw new InvalidDataException("全场对象表缺少快照对象 ID " + key + "。");
                }

                result.Add(new KeyValuePair<int, ObjectCtrlInfo>(key, value));
            }

            return result;
        }

        private void EnsureTimelineAutoplayIsIgnore()
        {
            if (!_timelineInstalled)
            {
                return;
            }

            object configEntry = _timelineConfigAutoplayProperty.GetValue(null, null);
            PropertyInfo valueProperty = configEntry == null
                ? null
                : AccessTools.Property(configEntry.GetType(), "Value");
            object value = valueProperty == null ? null : valueProperty.GetValue(configEntry, null);
            if (value == null ||
                !string.Equals(value.ToString(), "Ignore", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "为保证本插件不参与 Timeline 播放/暂停控制，请先把 Timeline 的 Autoplay 设为 Ignore。当前值：" +
                    (value == null ? "不可读取" : value.ToString()));
            }
        }

        private IEnumerator ReplayTimeline(Transaction transaction, Action<Exception> onError)
        {
            if (!_timelineInstalled)
            {
                yield break;
            }

            if (!transaction.TimelineSceneLoadDeferred || transaction.DeferredTimelineNode == null)
            {
                onError(new InvalidDataException("没有捕获到 Timeline 场景轨道数据。"));
                yield break;
            }

            object timeline = transaction.DeferredTimelineInstance ?? _timelineSelfField.GetValue(null);
            if (timeline == null)
            {
                onError(new InvalidOperationException("未取得 Timeline 运行实例。"));
                yield break;
            }

            ResetTimelineReplayState(transaction);
            try
            {
                EnsureTimelineAutoplayIsIgnore();
                ClearTimelineRuntime();
                List<KeyValuePair<int, ObjectCtrlInfo>> objectList = BuildStableObjectList(transaction);
                XmlNode replayNode = transaction.DeferredTimelineNode.CloneNode(true);
                transaction.TimelinePathsRemapped = 0;
                RewriteTimelineTargetPaths(replayNode, transaction, objectList);
                _timelineCoreSceneLoadMethod.Invoke(timeline, new object[]
                {
                    replayNode,
                    objectList
                });
                _timelineUpdateInterpolablesViewMethod.Invoke(timeline, null);
                _timelineCloseKeyframeWindowMethod.Invoke(timeline, null);
            }
            catch (Exception error)
            {
                transaction.TimelineAdapterError = UnwrapInvocationException(error).Message;
            }

            if (!transaction.TimelineLoadCompleted &&
                string.IsNullOrEmpty(transaction.TimelineAdapterError))
            {
                transaction.TimelineAdapterError =
                    "Timeline 核心重绑调用返回后未报告完成状态。";
            }

            if (!string.IsNullOrEmpty(transaction.TimelineAdapterError))
            {
                ClearTimelineRuntimeBestEffort();
                onError(new InvalidDataException(transaction.TimelineAdapterError));
            }
        }

        private IEnumerator RebindExtensionsAfterSettle(Transaction transaction,
            bool auditConvertedEndpoints, Action<Exception> onError)
        {
            Exception lastError = null;
            for (int attempt = 1; attempt <= 2; attempt++)
            {
                lastError = ReplayNodesConstraints(transaction, auditConvertedEndpoints);
                if (lastError != null)
                {
                    break;
                }

                yield return DrainCoroutine(ReplayTimeline(transaction,
                        delegate(Exception error) { lastError = error; }),
                    delegate(Exception error) { lastError = error; });

                if (lastError == null)
                {
                    _log.LogInfo(string.Format(
                        "扩展重绑检查通过：NodesConstraints {0} 条，Timeline {1}/{2} 条，模式={3}。",
                        transaction.SavedConstraintCount < 0 ? 0 : transaction.SavedConstraintCount,
                        transaction.LoadedTimelineTrackCount,
                        transaction.ExpectedTimelineTrackCount < 0 ? 0 : transaction.ExpectedTimelineTrackCount,
                        transaction.LoadingRecovery ? "恢复" : "转换"));
                    yield break;
                }

                ClearTimelineRuntimeBestEffort();
                ClearNodesConstraintsRuntimeBestEffort();
                if (attempt < 2)
                {
                    SetStatus("检测到骨架仍在变化，正在等待全场稳定后再次重绑……", 60f);
                    Exception settleError = null;
                    yield return DrainCoroutine(WaitForSceneBoneGraphs(transaction,
                            delegate(Exception error) { settleError = error; }),
                        delegate(Exception error) { settleError = error; });
                    if (settleError != null)
                    {
                        lastError = settleError;
                        break;
                    }
                }
            }

            onError(lastError ?? new InvalidDataException("扩展插件重绑失败。"));
        }

        private static int CountTimelineTracks(XmlNode root)
        {
            if (root == null)
            {
                return 0;
            }

            XmlNodeList nodes = root.SelectNodes(".//interpolable");
            return nodes == null ? 0 : nodes.Count;
        }

        private static bool IsHs2PeBoneTrack(string owner, string id)
        {
            return owner.IndexOf("HS2PE", StringComparison.OrdinalIgnoreCase) >= 0 &&
                   (string.Equals(id, "bonePos", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(id, "boneRot", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(id, "boneScale", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsBuiltInGuideObjectTrack(string owner, string id)
        {
            return string.Equals(owner, "Timeline", StringComparison.OrdinalIgnoreCase) &&
                   (string.Equals(id, "guideObjectPos", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(id, "guideObjectRot", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(id, "guideObjectScale", StringComparison.OrdinalIgnoreCase));
        }

        private static void RewriteTimelineTargetPaths(XmlNode timelineRoot, Transaction transaction,
            IList<KeyValuePair<int, ObjectCtrlInfo>> objects)
        {
            if (transaction.LoadingRecovery || timelineRoot == null || objects == null)
            {
                return;
            }

            XmlNodeList tracks = timelineRoot.SelectNodes(".//interpolable");
            if (tracks == null)
            {
                return;
            }

            foreach (XmlNode track in tracks)
            {
                XmlAttributeCollection attributes = track.Attributes;
                if (attributes == null || attributes["objectIndex"] == null ||
                    attributes["owner"] == null || attributes["id"] == null)
                {
                    continue;
                }

                int objectIndex;
                if (!int.TryParse(attributes["objectIndex"].Value, out objectIndex) ||
                    objectIndex < 0 || objectIndex >= objects.Count ||
                    !transaction.Keys.Contains(objects[objectIndex].Key))
                {
                    continue;
                }

                string owner = attributes["owner"].Value ?? string.Empty;
                string id = attributes["id"].Value ?? string.Empty;
                XmlAttribute pathAttribute = IsHs2PeBoneTrack(owner, id)
                    ? attributes["parameter"]
                    : IsBuiltInGuideObjectTrack(owner, id)
                        ? attributes["guideObjectPath"]
                        : null;
                if (pathAttribute == null || string.IsNullOrEmpty(pathAttribute.Value))
                {
                    continue;
                }

                ObjectCtrlInfo oci = objects[objectIndex].Value;
                Transform root = oci == null || oci.guideObject == null
                    ? null
                    : oci.guideObject.transformTarget;
                if (root == null || root.Find(pathAttribute.Value) != null)
                {
                    continue;
                }

                string mappedPath;
                if (!TryMapRelativeTransformPath(root, pathAttribute.Value, out mappedPath))
                {
                    continue;
                }

                string oldPath = pathAttribute.Value;
                pathAttribute.Value = mappedPath;
                transaction.TimelinePathsRemapped++;
                _log.LogInfo(string.Format(
                    "Timeline {0}/{1} 路径已映射：对象 {2}，{3} -> {4}",
                    owner, id, objects[objectIndex].Key, oldPath, mappedPath));
            }
        }

        private static int CountSavableTimelineTracks(object timeline)
        {
            IDictionary tracks = _instance._timelineInterpolablesField.GetValue(timeline) as IDictionary;
            if (tracks == null)
            {
                throw new InvalidOperationException("Timeline._interpolables 不可读取。");
            }

            int count = 0;
            foreach (DictionaryEntry entry in tracks)
            {
                object track = entry.Value;
                if (track == null)
                {
                    continue;
                }

                FieldInfo keyframesField = FindFieldInHierarchy(track.GetType(), "keyframes");
                IDictionary keyframes = keyframesField == null
                    ? null
                    : keyframesField.GetValue(track) as IDictionary;
                if (keyframes == null || keyframes.Count == 0)
                {
                    continue;
                }

                FieldInfo ociField = FindFieldInHierarchy(track.GetType(), "oci");
                ObjectCtrlInfo oci = ociField == null ? null : ociField.GetValue(track) as ObjectCtrlInfo;
                if (oci != null && (!Singleton<StudioCore>.IsInstance() ||
                                    !Singleton<StudioCore>.Instance.dicObjectCtrl.Values.Any(
                                        delegate(ObjectCtrlInfo value)
                                        {
                                            return ReferenceEquals(value, oci);
                                        })))
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        private static void AuditTimelineBindings(object timeline, Transaction transaction,
            out int loadedCount, out int invalidCount, out string detail)
        {
            loadedCount = 0;
            invalidCount = 0;
            List<string> problems = new List<string>();
            if (_instance == null || !_instance._timelineInstalled)
            {
                detail = string.Empty;
                return;
            }

            timeline = timeline ?? _instance._timelineSelfField.GetValue(null);
            IDictionary tracks = timeline == null
                ? null
                : _instance._timelineInterpolablesField.GetValue(timeline) as IDictionary;
            if (tracks == null)
            {
                throw new InvalidOperationException("Timeline._interpolables 不可读取。");
            }

            loadedCount = tracks.Count;
            foreach (DictionaryEntry entry in tracks)
            {
                object track = entry.Value;
                string problem;
                if (!IsTimelineTrackBindingAlive(track, out problem))
                {
                    invalidCount++;
                    if (problems.Count < 4)
                    {
                        problems.Add("轨道 " + entry.Key + "：" + problem);
                    }
                }
            }

            IList pendingDelete = _instance._timelineToDeleteField.GetValue(timeline) as IList;
            if (pendingDelete == null)
            {
                throw new InvalidOperationException("Timeline._toDelete 不可读取。");
            }

            if (pendingDelete.Count > 0)
            {
                invalidCount += pendingDelete.Count;
                problems.Add("待删除轨道 " + pendingDelete.Count + " 条");
            }

            detail = problems.Count == 0 ? string.Empty : string.Join("；", problems.ToArray());
        }

        private static bool IsTimelineTrackBindingAlive(object track, out string problem)
        {
            if (track == null)
            {
                problem = "轨道对象为空";
                return false;
            }

            FieldInfo ociField = FindFieldInHierarchy(track.GetType(), "oci");
            FieldInfo ownerField = FindFieldInHierarchy(track.GetType(), "owner");
            FieldInfo idField = FindFieldInHierarchy(track.GetType(), "id");
            FieldInfo parameterField = FindFieldInHierarchy(track.GetType(), "parameter");
            if (ociField == null || ownerField == null || idField == null || parameterField == null)
            {
                problem = "轨道模型字段不完整";
                return false;
            }

            ObjectCtrlInfo oci = ociField.GetValue(track) as ObjectCtrlInfo;
            if (oci != null)
            {
                bool belongsToScene = Singleton<StudioCore>.IsInstance() &&
                                      Singleton<StudioCore>.Instance.dicObjectCtrl.Values.Any(
                                          delegate(ObjectCtrlInfo value)
                                          {
                                              return ReferenceEquals(value, oci);
                                          });
                if (!belongsToScene || oci.guideObject == null ||
                    oci.guideObject.transformTarget == null)
                {
                    problem = "OCI 或角色根节点已失效";
                    return false;
                }
            }

            string owner = ownerField.GetValue(track) as string ?? string.Empty;
            string id = idField.GetValue(track) as string ?? string.Empty;
            object parameter = parameterField.GetValue(track);
            PropertyInfo nameProperty = track.GetType().GetProperty("name",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (nameProperty != null)
            {
                try
                {
                    nameProperty.GetValue(track, null);
                }
                catch (Exception error)
                {
                    problem = owner + "/" + id + " 的名称解析异常：" +
                              UnwrapInvocationException(error).Message;
                    return false;
                }
            }

            if (IsDestroyedUnityObject(parameter))
            {
                problem = owner + "/" + id + " 的 Unity 参数已销毁";
                return false;
            }

            if (IsBuiltInGuideObjectTrack(owner, id))
            {
                GuideObject guide = parameter as GuideObject;
                if (guide == null || guide.transformTarget == null)
                {
                    problem = id + " 的 GuideObject 已失效";
                    return false;
                }

                IDictionary registry = Singleton<GuideObjectManager>.IsInstance()
                    ? GuideObjectDictionaryField.GetValue(Singleton<GuideObjectManager>.Instance) as IDictionary
                    : null;
                Transform root = oci == null || oci.guideObject == null
                    ? null
                    : oci.guideObject.transformTarget;
                if (!IsGuideObjectRegistered(registry, guide, guide.transformTarget) ||
                    (root != null && !IsTransformUnderRoot(guide.transformTarget, root)))
                {
                    problem = id + " 未绑定到当前 OCI 的 GuideObject 注册表";
                    return false;
                }
            }

            if (IsHs2PeBoneTrack(owner, id))
            {
                string hs2PeProblem;
                if (!ValidateHashedBoneParameter(parameter, oci, out hs2PeProblem))
                {
                    problem = owner + "/" + id + "：" + hs2PeProblem;
                    return false;
                }
            }

            if (owner.IndexOf("NodesConstraints", StringComparison.OrdinalIgnoreCase) >= 0 &&
                id.IndexOf("constraint", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                string constraintProblem;
                if (!ValidateConstraintParameter(parameter, out constraintProblem))
                {
                    problem = owner + "/" + id + "：" + constraintProblem;
                    return false;
                }
            }

            MethodInfo integrityMethod = AccessTools.Method(track.GetType(), "CheckIntegrity",
                new[] { typeof(object), typeof(object) });
            object sampleValue;
            if (integrityMethod != null && TryGetFirstTimelineValue(track, out sampleValue))
            {
                try
                {
                    object integrityResult = integrityMethod.Invoke(track,
                        new[] { sampleValue, sampleValue });
                    if (integrityResult is bool && !(bool)integrityResult)
                    {
                        problem = owner + "/" + id + " 的插件完整性检查未通过";
                        return false;
                    }
                }
                catch (Exception error)
                {
                    problem = owner + "/" + id + " 的完整性检查异常：" +
                              UnwrapInvocationException(error).Message;
                    return false;
                }
            }

            problem = string.Empty;
            return true;
        }

        private static bool TryGetFirstTimelineValue(object track, out object value)
        {
            value = null;
            FieldInfo keyframesField = FindFieldInHierarchy(track.GetType(), "keyframes");
            IEnumerable keyframes = keyframesField == null
                ? null
                : keyframesField.GetValue(track) as IEnumerable;
            if (keyframes == null)
            {
                return false;
            }

            foreach (object pair in keyframes)
            {
                PropertyInfo pairValue = pair == null ? null : pair.GetType().GetProperty("Value");
                object keyframe = pairValue == null ? null : pairValue.GetValue(pair, null);
                FieldInfo valueField = keyframe == null
                    ? null
                    : FindFieldInHierarchy(keyframe.GetType(), "value");
                if (valueField != null)
                {
                    value = valueField.GetValue(keyframe);
                    return true;
                }
            }

            return false;
        }

        private static bool ValidateHashedBoneParameter(object parameter, ObjectCtrlInfo oci,
            out string problem)
        {
            if (parameter == null || oci == null || oci.guideObject == null ||
                oci.guideObject.transformTarget == null)
            {
                problem = "骨骼参数或当前 OCI 根为空";
                return false;
            }

            if (parameter.GetType().Name.IndexOf("HashedPair", StringComparison.OrdinalIgnoreCase) < 0)
            {
                problem = "骨骼参数不是 HashedPair";
                return false;
            }

            object editor = null;
            Transform transform = null;
            foreach (FieldInfo field in GetInstanceFields(parameter))
            {
                object value = field.GetValue(parameter);
                if (string.Equals(field.Name, "key", StringComparison.OrdinalIgnoreCase))
                {
                    editor = value;
                }
                else if (string.Equals(field.Name, "value", StringComparison.OrdinalIgnoreCase) ||
                         typeof(Transform).IsAssignableFrom(field.FieldType))
                {
                    transform = value as Transform;
                }
            }

            Transform root = oci.guideObject.transformTarget;
            Component poseController = _hspePoseControllerType == null
                ? null
                : root.GetComponent(_hspePoseControllerType);
            object currentEditor = poseController == null
                ? null
                : _hspeBonesEditorField.GetValue(poseController);
            if (editor == null || currentEditor == null || !ReferenceEquals(editor, currentEditor))
            {
                problem = "BonesEditor 不是当前 OCI 的实例";
                return false;
            }

            if (!IsTransformUnderRoot(transform, root))
            {
                problem = "目标 Transform 不属于当前 OCI 根";
                return false;
            }

            problem = string.Empty;
            return true;
        }

        private static bool ValidateConstraintParameter(object parameter, out string problem)
        {
            if (parameter == null)
            {
                problem = "约束参数为空";
                return false;
            }

            FieldInfo parentField = FindFieldInHierarchy(parameter.GetType(), "parentTransform");
            FieldInfo childField = FindFieldInHierarchy(parameter.GetType(), "childTransform");
            if (parentField == null || childField == null)
            {
                problem = "约束 Transform 字段不存在";
                return false;
            }

            Transform parent = parentField.GetValue(parameter) as Transform;
            Transform child = childField.GetValue(parameter) as Transform;
            if (parent == null || child == null)
            {
                problem = "父级或子级 Transform 已失效";
                return false;
            }

            problem = string.Empty;
            return true;
        }

        private static IEnumerable<FieldInfo> GetInstanceFields(object value)
        {
            if (value == null)
            {
                return Enumerable.Empty<FieldInfo>();
            }

            List<FieldInfo> fields = new List<FieldInfo>();
            Type current = value.GetType();
            while (current != null && current != typeof(object))
            {
                fields.AddRange(current.GetFields(BindingFlags.Instance | BindingFlags.Public |
                                                  BindingFlags.NonPublic | BindingFlags.DeclaredOnly));
                current = current.BaseType;
            }

            return fields;
        }

        private static FieldInfo FindFieldInHierarchy(Type type, string name)
        {
            Type current = type;
            while (current != null)
            {
                FieldInfo field = current.GetField(name, BindingFlags.Instance | BindingFlags.Static |
                                                         BindingFlags.Public | BindingFlags.NonPublic |
                                                         BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return field;
                }

                current = current.BaseType;
            }

            return null;
        }

        private static bool IsDestroyedUnityObject(object value)
        {
            UnityEngine.Object unityObject = value as UnityEngine.Object;
            return !ReferenceEquals(unityObject, null) && unityObject == null;
        }

        private static Exception UnwrapInvocationException(Exception error)
        {
            Exception current = error;
            while (current is TargetInvocationException && current.InnerException != null)
            {
                current = current.InnerException;
            }

            return current;
        }

        private static void ValidateConstraintDocumentCount(XmlNode root, Transaction transaction)
        {
            if (root == null)
            {
                throw new InvalidDataException("NodesConstraints 未提供有效的约束 XML。");
            }

            if (transaction.SavedConstraintCount >= 0 &&
                root.ChildNodes.Count != transaction.SavedConstraintCount)
            {
                throw new InvalidDataException(string.Format(
                    "NodesConstraints 保存 {0} 条、场景快照读出 {1} 条，扩展数据不完整。",
                    transaction.SavedConstraintCount, root.ChildNodes.Count));
            }
        }

        private static void AuditNodesConstraintsAfterSettle(Transaction transaction,
            bool auditConvertedEndpoints)
        {
            if (transaction.SavedConstraintCount < 0 ||
                !string.IsNullOrEmpty(transaction.ConstraintAdapterError))
            {
                return;
            }

            try
            {
                if (!transaction.ConstraintLoadObserved || !transaction.ConstraintLoadCompleted)
                {
                    throw new InvalidDataException(
                        "NodesConstraints 载入回调未完整执行，绑骨数据可能缺失。");
                }

                if (transaction.NodesConstraintsInstance == null)
                {
                    throw new InvalidOperationException("未取得 NodesConstraints 运行实例。");
                }

                FieldInfo constraintsField = AccessTools.Field(
                    transaction.NodesConstraintsInstance.GetType(), "_constraints");
                System.Collections.IList constraints = constraintsField == null
                    ? null
                    : constraintsField.GetValue(transaction.NodesConstraintsInstance) as System.Collections.IList;
                if (constraints == null)
                {
                    throw new MissingFieldException("NodesConstraints._constraints 不可读取。");
                }

                if (constraints.Count != transaction.SavedConstraintCount)
                {
                    throw new InvalidDataException(string.Format(
                        "NodesConstraints 应恢复 {0} 条，稳定后实际为 {1} 条。",
                        transaction.SavedConstraintCount, constraints.Count));
                }

                if (!auditConvertedEndpoints)
                {
                    return;
                }

                foreach (int expectedId in transaction.ExpectedConstraintIds)
                {
                    object constraint;
                    if (!transaction.LoadedConstraintObjects.TryGetValue(expectedId, out constraint) ||
                        constraint == null || !constraints.Contains(constraint))
                    {
                        transaction.FailedConstraintIds.Add(expectedId);
                        continue;
                    }

                    FieldInfo parentField = AccessTools.Field(constraint.GetType(), "parentTransform");
                    FieldInfo childField = AccessTools.Field(constraint.GetType(), "childTransform");
                    if (parentField == null || childField == null)
                    {
                        throw new MissingFieldException("NodesConstraints.Constraint 的 Transform 字段不可读取。");
                    }

                    Transform parent = parentField.GetValue(constraint) as Transform;
                    Transform child = childField.GetValue(constraint) as Transform;
                    if (parent == null || child == null)
                    {
                        transaction.FailedConstraintIds.Add(expectedId);
                    }
                }
            }
            catch (Exception error)
            {
                transaction.ConstraintAdapterError = error.Message;
                _log.LogError("NodesConstraints 延迟稳定性校验失败：" + error);
            }
        }

        private static Exception NodesConstraintsLoadSceneFinalizer(Exception __exception)
        {
            if (__exception != null && _instance != null && _instance._activeTransaction != null)
            {
                _instance._activeTransaction.ConstraintAdapterError =
                    UnwrapInvocationException(__exception).Message;
                _log.LogError("NodesConstraints 原始载入方法发生异常：" + __exception);
                return null;
            }

            return __exception;
        }

        private void RewriteNodesConstraintPaths(XmlNode root,
            IList<KeyValuePair<int, ObjectCtrlInfo>> objects, Transaction transaction)
        {
            if (root == null || objects == null)
            {
                throw new InvalidDataException("NodesConstraints 未提供有效的约束 XML 或对象表。");
            }

            transaction.ExpectedConstraintIds.Clear();
            transaction.FailedConstraintIds.Clear();
            transaction.LoadedConstraintObjects.Clear();
            transaction.NodesConstraintsInstance = null;
            transaction.UnidentifiedConstraints = 0;
            transaction.ConstraintPathsRemapped = 0;
            if (transaction.SavedConstraintCount >= 0 &&
                root.ChildNodes.Count != transaction.SavedConstraintCount)
            {
                throw new InvalidDataException(string.Format(
                    "NodesConstraints 保存 {0} 条、转换快照读出 {1} 条，扩展数据不完整。",
                    transaction.SavedConstraintCount, root.ChildNodes.Count));
            }

            int ordinal = 0;
            foreach (XmlNode constraint in root.ChildNodes)
            {
                bool touchesConverted = ConstraintEndpointTouchesConverted(constraint,
                                            "parentObjectIndex", objects, transaction) ||
                                        ConstraintEndpointTouchesConverted(constraint,
                                            "childObjectIndex", objects, transaction);
                int loadId = 0;
                bool hasLoadId = constraint.Attributes != null &&
                                 constraint.Attributes["uniqueLoadId"] != null &&
                                 int.TryParse(constraint.Attributes["uniqueLoadId"].Value, out loadId);
                if (touchesConverted)
                {
                    if (hasLoadId)
                    {
                        transaction.ExpectedConstraintIds.Add(loadId);
                    }
                    else
                    {
                        transaction.UnidentifiedConstraints++;
                        _log.LogWarning("涉及被替换角色的 NodesConstraints 约束缺少 uniqueLoadId，无法验证是否成功重建。序号：" + ordinal);
                    }
                }

                int parentResult = RewriteConstraintEndpoint(constraint, "parentObjectIndex", "parentPath",
                    objects, transaction);
                int childResult = RewriteConstraintEndpoint(constraint, "childObjectIndex", "childPath",
                    objects, transaction);
                transaction.ConstraintPathsRemapped += Math.Max(0, parentResult) + Math.Max(0, childResult);
                if (touchesConverted && (parentResult < 0 || childResult < 0))
                {
                    if (hasLoadId)
                    {
                        transaction.FailedConstraintIds.Add(loadId);
                    }
                    else
                    {
                        // 上方已按约束而不是按端点计数；这里不重复累加。
                    }
                }

                ordinal++;
            }
        }

        private static bool ConstraintEndpointTouchesConverted(XmlNode constraint,
            string objectIndexName, IList<KeyValuePair<int, ObjectCtrlInfo>> objects,
            Transaction transaction)
        {
            XmlAttribute attribute = constraint.Attributes == null
                ? null
                : constraint.Attributes[objectIndexName];
            int objectIndex;
            return attribute != null && int.TryParse(attribute.Value, out objectIndex) &&
                   objectIndex >= 0 && objectIndex < objects.Count &&
                   transaction.Keys.Contains(objects[objectIndex].Key);
        }

        private static int RewriteConstraintEndpoint(XmlNode constraint, string objectIndexName,
            string pathName, IList<KeyValuePair<int, ObjectCtrlInfo>> objects, Transaction transaction)
        {
            XmlAttribute indexAttribute = constraint.Attributes == null
                ? null
                : constraint.Attributes[objectIndexName];
            XmlAttribute pathAttribute = constraint.Attributes == null ? null : constraint.Attributes[pathName];
            int objectIndex;
            if (indexAttribute == null || pathAttribute == null ||
                !int.TryParse(indexAttribute.Value, out objectIndex) || objectIndex < 0 ||
                objectIndex >= objects.Count)
            {
                return 0;
            }

            KeyValuePair<int, ObjectCtrlInfo> owner = objects[objectIndex];
            if (!transaction.Keys.Contains(owner.Key))
            {
                return 0;
            }

            Transform root = owner.Value == null || owner.Value.guideObject == null
                ? null
                : owner.Value.guideObject.transformTarget;
            if (root == null)
            {
                return -1;
            }

            string oldPath = pathAttribute.Value ?? string.Empty;
            if (oldPath.Length == 0 || root.Find(oldPath) != null)
            {
                return 0;
            }

            string mappedPath;
            if (!TryMapRelativeTransformPath(root, oldPath, out mappedPath))
            {
                _log.LogWarning("无法映射 NodesConstraints 节点：对象 " + owner.Key + "，路径 " + oldPath);
                return -1;
            }

            pathAttribute.Value = mappedPath;
            _log.LogInfo("NodesConstraints 路径已映射：" + oldPath + " -> " + mappedPath);
            return 1;
        }

        private static bool TryMapRelativeTransformPath(Transform root, string oldPath,
            out string mappedPath)
        {
            mappedPath = string.Empty;
            bool hitObjectPath = oldPath.IndexOf("p_cm_body_hit_low", StringComparison.Ordinal) >= 0;
            string direct = oldPath
                .Replace("p_cm_body_00", "p_cf_body_00")
                .Replace("n_body_cm", "n_body_cf")
                .Replace("o_body_cm", "o_body_cf")
                .Replace("p_cm_body_silhouette", "p_cf_body_silhouette")
                .Replace("o_silhouette_cm", "o_silhouette_cf")
                .Replace("p_cm_body_hit_low", "p_cf_body_00_hit_low")
                .Replace("N_hit_cm_low", "N_hit_cf_low");
            if (hitObjectPath)
            {
                string[] hitParts = direct.Split('/');
                for (int i = 0; i < hitParts.Length; i++)
                {
                    if (hitParts[i].StartsWith("O_hit_", StringComparison.Ordinal) &&
                        hitParts[i].EndsWith("_cm", StringComparison.Ordinal))
                    {
                        hitParts[i] = hitParts[i].Substring(0, hitParts[i].Length - 3);
                    }
                }

                direct = string.Join("/", hitParts);
            }

            if (!string.Equals(direct, oldPath, StringComparison.Ordinal) && root.Find(direct) != null)
            {
                mappedPath = direct;
                return true;
            }

            return false;
        }

        private bool TryPrepareTransaction(IList<int> requestedKeys, string femaleCardPath,
            out Transaction transaction, out Exception error)
        {
            transaction = null;
            error = null;

            try
            {
                if (!Singleton<StudioCore>.IsInstance())
                {
                    throw new InvalidOperationException("Studio 尚未初始化。");
                }

                if (string.IsNullOrEmpty(femaleCardPath) || !File.Exists(femaleCardPath))
                {
                    throw new FileNotFoundException("找不到所选女角色卡。", femaleCardPath);
                }

                StudioCore studio = Singleton<StudioCore>.Instance;
                EnsureTimelineAutoplayIsIgnore();
                List<OCIChar> targets = new List<OCIChar>();
                for (int i = 0; i < requestedKeys.Count; i++)
                {
                    OCIChar target = StudioCore.GetCtrlInfo(requestedKeys[i]) as OCIChar;
                    if (target != null && target.oiCharInfo != null && target.oiCharInfo.sex == 0 &&
                        !targets.Contains(target))
                    {
                        targets.Add(target);
                    }
                }

                if (targets.Count == 0)
                {
                    throw new InvalidOperationException("当前选择中没有可替换的男角色。");
                }

                Transaction prepared = new Transaction();
                prepared.CardPath = femaleCardPath;
                prepared.ExpectedObjectKeys.AddRange(studio.dicObjectCtrl.Keys.OrderBy(
                    delegate(int key) { return key; }));
                prepared.ExpectedCharacterCount = studio.dicObjectCtrl.Values.Count(
                    delegate(ObjectCtrlInfo value) { return value is OCIChar; });
                prepared.Keys.AddRange(targets.Select(delegate(OCIChar value)
                {
                    return value.objectInfo.dicKey;
                }));

                for (int i = 0; i < targets.Count; i++)
                {
                    ChaFileControl femaleFile = new ChaFileControl();
                    if (!femaleFile.LoadCharaFile(femaleCardPath, 1, true, true) ||
                        femaleFile.parameter == null || femaleFile.parameter.sex != 1)
                    {
                        throw new InvalidDataException("所选文件不是有效的 HS2 女角色卡：" + femaleCardPath);
                    }

                    OCIChar target = targets[i];
                    prepared.Mutations.Add(new InfoMutation
                    {
                        Info = target.oiCharInfo,
                        OriginalSex = target.oiCharInfo.sex,
                        OriginalFile = target.oiCharInfo.charFile,
                        FemaleFile = femaleFile
                    });
                    prepared.Bones[target.objectInfo.dicKey] = CaptureBones(target);
                }

                string cacheDirectory = Path.Combine(Paths.CachePath, "HS2_FemaleForMale");
                Directory.CreateDirectory(cacheDirectory);
                prepared.RecoveryPath = Path.Combine(cacheDirectory, "LastRecovery.png");
                prepared.ConvertedPath = Path.Combine(cacheDirectory, "LastConverted.png");

                PrepareSceneForSave(studio);
                if (!SaveSnapshotWithConstraintValidation(studio, prepared.RecoveryPath, prepared) ||
                    !IsUsableSnapshot(prepared.RecoveryPath))
                {
                    throw new IOException("转换前恢复快照写入失败，当前场景未作修改。");
                }

                bool convertedSaved = false;
                try
                {
                    for (int i = 0; i < prepared.Mutations.Count; i++)
                    {
                        ApplyFemaleMutation(prepared.Mutations[i]);
                    }

                    convertedSaved = SaveSnapshotWithConstraintValidation(
                                         studio, prepared.ConvertedPath, prepared) &&
                                      IsUsableSnapshot(prepared.ConvertedPath);
                }
                finally
                {
                    for (int i = prepared.Mutations.Count - 1; i >= 0; i--)
                    {
                        RestoreMutation(prepared.Mutations[i]);
                    }
                }

                if (!convertedSaved)
                {
                    throw new IOException("转换场景快照写入失败，当前场景已恢复原始数据。");
                }

                transaction = prepared;
                return true;
            }
            catch (Exception caught)
            {
                error = caught;
                return false;
            }
        }

        private bool SaveSnapshotWithConstraintValidation(StudioCore studio, string path,
            Transaction transaction)
        {
            transaction.ConstraintSaveObserved = false;
            transaction.ConstraintSaveCompleted = false;
            transaction.ConstraintSaveError = string.Empty;
            transaction.TimelineSaveObserved = false;
            transaction.TimelineSaveCompleted = false;
            transaction.TimelineSaveError = string.Empty;
            transaction.TimelineRuntimeTrackCount = -1;
            transaction.TimelineSnapshotTrackCount = -1;
            _snapshotTransaction = transaction;
            bool saved;
            try
            {
                saved = studio.sceneInfo.Save(path);
            }
            finally
            {
                _snapshotTransaction = null;
            }

            if (_nodesConstraintsInstalled)
            {
                if (!string.IsNullOrEmpty(transaction.ConstraintSaveError))
                {
                    throw new InvalidDataException(
                        "NodesConstraints 未能写入完整绑骨数据：" + transaction.ConstraintSaveError);
                }

                if (!transaction.ConstraintSaveObserved || !transaction.ConstraintSaveCompleted)
                {
                    throw new InvalidDataException(
                        "NodesConstraints 保存回调未完整执行，已在重载场景前停止替换。");
                }
            }


            if (_timelineInstalled)
            {
                if (!string.IsNullOrEmpty(transaction.TimelineSaveError))
                {
                    throw new InvalidDataException(
                        "Timeline 未能写入完整轨道数据：" + transaction.TimelineSaveError);
                }

                if (!transaction.TimelineSaveObserved || !transaction.TimelineSaveCompleted)
                {
                    throw new InvalidDataException(
                        "Timeline 保存回调未完整执行，已在清空旧轨道前停止替换。");
                }
            }

            return saved;
        }

        private static void PrepareSceneForSave(StudioCore studio)
        {
            ObjectCtrlInfo[] objects = studio.dicObjectCtrl.Values.Distinct().ToArray();
            for (int i = 0; i < objects.Length; i++)
            {
                objects[i].OnSavePreprocessing();
            }

            studio.sceneInfo.cameraSaveData = studio.cameraCtrl.Export();
        }

        private static bool IsUsableSnapshot(string path)
        {
            FileInfo info = new FileInfo(path);
            return info.Exists && info.Length > 1024L;
        }

        private static void ApplyFemaleMutation(InfoMutation mutation)
        {
            SexProperty.GetSetMethod(true).Invoke(mutation.Info, new object[] { 1 });
            CharFileProperty.GetSetMethod(true).Invoke(mutation.Info, new object[] { mutation.FemaleFile });

            for (int i = 0; i < MaleOnlyBoneIds.Length; i++)
            {
                OIBoneInfo maleOnly;
                if (mutation.Info.bones.TryGetValue(MaleOnlyBoneIds[i], out maleOnly))
                {
                    mutation.RemovedMaleBones.Add(MaleOnlyBoneIds[i], maleOnly);
                    mutation.Info.bones.Remove(MaleOnlyBoneIds[i]);
                }
            }
        }

        private static void RestoreMutation(InfoMutation mutation)
        {
            CharFileProperty.GetSetMethod(true).Invoke(mutation.Info, new object[] { mutation.OriginalFile });
            SexProperty.GetSetMethod(true).Invoke(mutation.Info, new object[] { mutation.OriginalSex });

            foreach (KeyValuePair<int, OIBoneInfo> pair in mutation.RemovedMaleBones)
            {
                mutation.Info.bones[pair.Key] = pair.Value;
            }
        }

        private static BoneSnapshot CaptureBones(OCIChar character)
        {
            BoneSnapshot snapshot = new BoneSnapshot();
            for (int i = 0; i < character.listBones.Count; i++)
            {
                OCIChar.BoneInfo bone = character.listBones[i];
                if (bone == null || bone.boneInfo == null || bone.guideObject == null ||
                    bone.guideObject.transformTarget == null)
                {
                    continue;
                }

                BoneSignature existing;
                if (snapshot.ById.TryGetValue(bone.boneID, out existing))
                {
                    existing.Unique = false;
                    snapshot.ById[bone.boneID] = existing;
                    continue;
                }

                Transform target = bone.guideObject.transformTarget;
                snapshot.ById.Add(bone.boneID, new BoneSignature
                {
                    Unique = true,
                    Name = target.name,
                    ParentSuffix = BuildParentSuffix(target, 4),
                    Group = bone.boneGroup,
                    Rotation = bone.boneInfo.changeAmount.rot,
                    BoneWeight = bone.boneWeight
                });
            }

            return snapshot;
        }

        private static IEnumerator WaitForSceneBoneGraphs(Transaction transaction,
            Action<Exception> onError)
        {
            yield return new WaitForSecondsRealtime(1.25f);

            float quietTimeout = _boneGraphQuietTimeoutSeconds == null
                ? 45f
                : Mathf.Clamp(_boneGraphQuietTimeoutSeconds.Value, 10f, 180f);
            float startedAt = Time.realtimeSinceStartup;
            float deadline = startedAt + quietTimeout;
            float hardDeadline = startedAt + quietTimeout * 3f;
            int stableFrames = 0;
            float permanentErrorSince = -1f;
            int previousFingerprint = int.MinValue;
            bool previousReady = false;
            string previousPermanentError = string.Empty;
            while (Time.realtimeSinceStartup < deadline &&
                   Time.realtimeSinceStartup < hardDeadline)
            {
                bool ready;
                int fingerprint = GetSceneBoneGraphFingerprint(transaction, out ready);
                string permanentError = transaction.BoneGraphPermanentError ?? string.Empty;
                bool progressed = fingerprint != previousFingerprint || ready != previousReady ||
                                  !string.Equals(permanentError, previousPermanentError,
                                      StringComparison.Ordinal);
                if (string.IsNullOrEmpty(permanentError))
                {
                    permanentErrorSince = -1f;
                    previousPermanentError = string.Empty;
                }
                else if (!progressed && string.Equals(permanentError, previousPermanentError,
                             StringComparison.Ordinal))
                {
                    if (permanentErrorSince < 0f)
                    {
                        permanentErrorSince = Time.realtimeSinceStartup;
                    }
                    else if (Time.realtimeSinceStartup - permanentErrorSince >= 5f)
                    {
                        onError(new InvalidDataException(permanentError));
                        yield break;
                    }
                }
                else
                {
                    permanentErrorSince = Time.realtimeSinceStartup;
                    previousPermanentError = permanentError;
                }

                if (progressed)
                {
                    deadline = Mathf.Min(hardDeadline,
                        Time.realtimeSinceStartup + quietTimeout);
                    stableFrames = 0;
                    previousFingerprint = fingerprint;
                    previousReady = ready;
                }
                else if (ready)
                {
                    stableFrames++;
                    if (stableFrames >= 45 && Time.frameCount - transaction.SceneLoadStartFrame >= 30)
                    {
                        transaction.SettledGraphFingerprint = fingerprint;
                        _log.LogInfo(string.Format(
                            "全场骨架图稳定：角色 {0}/{1}，指纹 {2}，模式={3}。",
                            GetSceneCharacterCount(), transaction.ExpectedCharacterCount, fingerprint,
                            transaction.LoadingRecovery ? "恢复" : "转换"));
                        yield break;
                    }
                }
                else
                {
                    stableFrames = 0;
                }

                yield return null;
            }

            float waited = Time.realtimeSinceStartup - startedAt;
            onError(new TimeoutException(string.Format(
                "全场角色的 FK、IK 与身体骨架等待 {0:0.0} 秒后仍未稳定（无进展时限 {1:0} 秒，总上限 {2:0} 秒）。",
                waited, quietTimeout, quietTimeout * 3f)));
        }

        private static int GetSceneBoneGraphFingerprint(Transaction transaction, out bool ready)
        {
            transaction.BoneGraphPermanentError = string.Empty;
            ready = Singleton<StudioCore>.IsInstance() &&
                    Singleton<GuideObjectManager>.IsInstance();
            unchecked
            {
                int hash = 17;
                if (!ready)
                {
                    return hash;
                }

                List<KeyValuePair<int, ObjectCtrlInfo>> objects =
                    Singleton<StudioCore>.Instance.dicObjectCtrl
                        .OrderBy(delegate(KeyValuePair<int, ObjectCtrlInfo> pair) { return pair.Key; })
                        .ToList();
                IDictionary guideRegistry = GuideObjectDictionaryField.GetValue(
                    Singleton<GuideObjectManager>.Instance) as IDictionary;
                if (guideRegistry == null)
                {
                    ready = false;
                    return hash;
                }

                hash = hash * 31 + objects.Count;
                hash = hash * 31 + guideRegistry.Count;
                bool hierarchyComplete = objects.Count == transaction.ExpectedObjectKeys.Count;
                if (!hierarchyComplete)
                {
                    ready = false;
                }

                int characterCount = 0;
                HashSet<int> hsPeObjectIndexes = GetHsPeObjectIndexes(transaction);
                for (int objectIndex = 0; objectIndex < objects.Count; objectIndex++)
                {
                    KeyValuePair<int, ObjectCtrlInfo> pair = objects[objectIndex];
                    ObjectCtrlInfo oci = pair.Value;
                    hash = hash * 31 + pair.Key;
                    hash = hash * 31 + RuntimeIdentity(oci);
                    if (objectIndex >= transaction.ExpectedObjectKeys.Count ||
                        transaction.ExpectedObjectKeys[objectIndex] != pair.Key || oci == null ||
                        oci.guideObject == null || oci.guideObject.transformTarget == null)
                    {
                        ready = false;
                        hierarchyComplete = false;
                        continue;
                    }

                    Transform objectRoot = oci.guideObject.transformTarget;
                    hash = hash * 31 + UnityInstanceId(oci.guideObject);
                    hash = hash * 31 + UnityInstanceId(objectRoot);
                    hash = hash * 31 + (oci.GetType().FullName ?? string.Empty).GetHashCode();
                    if (!IsGuideObjectRegistered(guideRegistry, oci.guideObject, objectRoot))
                    {
                        ready = false;
                    }

                    OCIChar character = oci as OCIChar;
                    if (character == null)
                    {
                        continue;
                    }

                    characterCount++;
                    if (character.oiCharInfo == null || character.charInfo == null)
                    {
                        ready = false;
                        hierarchyComplete = false;
                        continue;
                    }

                    hash = hash * 31 + character.oiCharInfo.sex;
                    hash = hash * 31 + UnityInstanceId(character.charInfo);
                    hash = hash * 31 + UnityInstanceId(character.charInfo.objBodyBone);
                    hash = hash * 31 + UnityInstanceId(character.charInfo.fullBodyIK);
                    hash = hash * 31 + UnityInstanceId(character.finalIK);
                    if (!character.charInfo.loadEnd || character.charInfo.objBodyBone == null ||
                        character.charInfo.fullBodyIK == null || character.finalIK == null)
                    {
                        ready = false;
                        if (!character.charInfo.loadEnd)
                        {
                            hierarchyComplete = false;
                        }
                    }

                    if (character.listBones == null || character.listBones.Count == 0)
                    {
                        ready = false;
                    }
                    else
                    {
                        hash = hash * 31 + character.listBones.Count;
                        for (int i = 0; i < character.listBones.Count; i++)
                        {
                            OCIChar.BoneInfo bone = character.listBones[i];
                            if (bone == null || bone.boneInfo == null || bone.guideObject == null ||
                                bone.guideObject.transformTarget == null)
                            {
                                ready = false;
                                hash = hash * 31;
                                continue;
                            }

                            Transform target = bone.guideObject.transformTarget;
                            hash = hash * 31 + bone.boneID;
                            hash = hash * 31 + (int)bone.boneGroup;
                            hash = hash * 31 + (bone.boneWeight ? 1 : 0);
                            hash = hash * 31 + bone.boneInfo.dicKey;
                            hash = hash * 31 + UnityInstanceId(bone.guideObject);
                            hash = hash * 31 + UnityInstanceId(target);
                            bool targetUnderRoot = IsTransformUnderRoot(target, objectRoot);
                            if (!targetUnderRoot)
                            {
                                ready = false;
                            }

                            if (targetUnderRoot && character.charInfo.loadEnd &&
                                !IsGuideObjectRegistered(guideRegistry, bone.guideObject, target))
                            {
                                string registrationProblem;
                                if (!TryRepairReusedBoneGuideRegistration(guideRegistry,
                                        bone.guideObject, target, pair.Key, bone.boneID,
                                        out registrationProblem))
                                {
                                    // An otherwise unregistered custom bone is allowed when no
                                    // Timeline track targets it. A concrete conflict or failed
                                    // stale-key repair is not safe and must stop the transaction.
                                    if (!string.IsNullOrEmpty(registrationProblem))
                                    {
                                        ready = false;
                                        if (string.IsNullOrEmpty(
                                                transaction.BoneGraphPermanentError))
                                        {
                                            transaction.BoneGraphPermanentError =
                                                registrationProblem;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (character.listIKTarget == null || character.listIKTarget.Count == 0)
                    {
                        ready = false;
                    }
                    else
                    {
                        hash = hash * 31 + character.listIKTarget.Count;
                        for (int i = 0; i < character.listIKTarget.Count; i++)
                        {
                            OCIChar.IKInfo ik = character.listIKTarget[i];
                            if (ik == null || ik.targetInfo == null || ik.guideObject == null ||
                                ik.guideObject.transformTarget == null || ik.baseObject == null ||
                                ik.targetObject == null || ik.boneObject == null)
                            {
                                ready = false;
                                hash = hash * 31;
                                continue;
                            }

                            Transform target = ik.guideObject.transformTarget;
                            hash = hash * 31 + (int)ik.boneGroup;
                            hash = hash * 31 + UnityInstanceId(ik.guideObject);
                            hash = hash * 31 + UnityInstanceId(target);
                            hash = hash * 31 + UnityInstanceId(ik.baseObject);
                            hash = hash * 31 + UnityInstanceId(ik.targetObject);
                            hash = hash * 31 + UnityInstanceId(ik.boneObject);
                            if (!IsGuideObjectRegistered(guideRegistry, ik.guideObject, target) ||
                                !IsTransformUnderRoot(target, objectRoot))
                            {
                                ready = false;
                            }
                        }
                    }

                    AppendAbmxFingerprint(character, objectRoot, ref hash, ref ready);
                    if (hsPeObjectIndexes.Contains(objectIndex))
                    {
                        AppendHsPeFingerprint(character, objectRoot, ref hash, ref ready);
                    }
                }

                hash = hash * 31 + characterCount;
                if (characterCount != transaction.ExpectedCharacterCount)
                {
                    ready = false;
                    hierarchyComplete = false;
                }

                bool nonTimelineGraphReady = hierarchyComplete && ready;
                AppendTimelinePathFingerprint(transaction, objects, guideRegistry,
                    nonTimelineGraphReady, ref hash, ref ready);
                return hash;
            }
        }

        private static bool IsGuideObjectRegistered(IDictionary registry, GuideObject guide,
            Transform target)
        {
            return registry != null && guide != null && target != null && registry.Contains(target) &&
                   ReferenceEquals(registry[target], guide);
        }

        private static bool TryRepairReusedBoneGuideRegistration(IDictionary registry,
            GuideObject guide, Transform target, int objectKey, int boneId, out string problem)
        {
            problem = string.Empty;
            if (registry == null || guide == null || target == null)
            {
                return false;
            }

            if (registry.Contains(target))
            {
                if (ReferenceEquals(registry[target], guide))
                {
                    return true;
                }

                problem = string.Format(
                    "对象 {0} 的扩展 FK 骨 {1} 目标 Transform 已被另一个 GuideObject 占用，无法安全修复注册表。",
                    objectKey, boneId);
                return false;
            }

            List<object> staleKeys = new List<object>();
            foreach (DictionaryEntry entry in registry)
            {
                if (ReferenceEquals(entry.Value, guide) && !ReferenceEquals(entry.Key, target))
                {
                    staleKeys.Add(entry.Key);
                }
            }

            // A stale key pointing to this exact GuideObject is the fingerprint of
            // AdditionalFKNodes reusing BoneInfo after ReloadCharacterBody. If no such key
            // exists, another plugin may still be registering the new guide, so keep waiting.
            if (staleKeys.Count == 0)
            {
                return false;
            }

            bool targetAdded = false;
            List<object> removedKeys = new List<object>();
            try
            {
                registry.Add(target, guide);
                targetAdded = true;
                for (int i = 0; i < staleKeys.Count; i++)
                {
                    registry.Remove(staleKeys[i]);
                    removedKeys.Add(staleKeys[i]);
                }

                if (!IsGuideObjectRegistered(registry, guide, target))
                {
                    throw new InvalidOperationException("写入后注册表未返回当前 GuideObject。");
                }

                for (int i = 0; i < staleKeys.Count; i++)
                {
                    if (registry.Contains(staleKeys[i]) &&
                        ReferenceEquals(registry[staleKeys[i]], guide))
                    {
                        throw new InvalidOperationException("旧 Transform 键仍指向当前 GuideObject。");
                    }
                }

                _log.LogInfo(string.Format(
                    "已修复 AdditionalFK 复用骨骼的 GuideObject 注册：对象 {0}，骨 ID {1}，目标 {2}。",
                    objectKey, boneId, target.name));
                return true;
            }
            catch (Exception error)
            {
                try
                {
                    if (targetAdded && registry.Contains(target) &&
                        ReferenceEquals(registry[target], guide))
                    {
                        registry.Remove(target);
                    }

                    for (int i = 0; i < removedKeys.Count; i++)
                    {
                        if (!registry.Contains(removedKeys[i]))
                        {
                            registry.Add(removedKeys[i], guide);
                        }
                    }
                }
                catch (Exception restoreError)
                {
                    _log.LogError("GuideObject 注册修复回退失败：" +
                                  UnwrapInvocationException(restoreError));
                }

                problem = string.Format(
                    "对象 {0} 的扩展 FK 骨 {1} GuideObject 注册修复失败：{2}",
                    objectKey, boneId, UnwrapInvocationException(error).Message);
                return false;
            }
        }

        private static bool IsTransformUnderRoot(Transform target, Transform root)
        {
            return target != null && root != null &&
                   (ReferenceEquals(target, root) || target.IsChildOf(root));
        }

        private static int RuntimeIdentity(object value)
        {
            UnityEngine.Object unityObject = value as UnityEngine.Object;
            return unityObject != null
                ? unityObject.GetInstanceID()
                : value == null
                    ? 0
                    : RuntimeHelpers.GetHashCode(value);
        }

        private static HashSet<int> GetHsPeObjectIndexes(Transaction transaction)
        {
            HashSet<int> result = new HashSet<int>();
            XmlNodeList tracks = transaction.DeferredTimelineNode == null
                ? null
                : transaction.DeferredTimelineNode.SelectNodes(".//interpolable");
            if (tracks == null)
            {
                return result;
            }

            foreach (XmlNode track in tracks)
            {
                XmlAttributeCollection attributes = track.Attributes;
                int objectIndex;
                if (attributes != null && attributes["owner"] != null &&
                    attributes["owner"].Value.IndexOf("HS2PE", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    attributes["objectIndex"] != null &&
                    int.TryParse(attributes["objectIndex"].Value, out objectIndex) && objectIndex >= 0)
                {
                    result.Add(objectIndex);
                }
            }

            return result;
        }

        private static void RefreshHsPeFkBones(Transaction transaction)
        {
            HashSet<int> objectIndexes = GetHsPeObjectIndexes(transaction);
            if (objectIndexes.Count == 0)
            {
                return;
            }

            if (_hspeCharaPoseControllerType == null)
            {
                throw new InvalidOperationException("场景含 HS2PE 轨道，但没有检测到 HS2PE 控制器。");
            }

            List<KeyValuePair<int, ObjectCtrlInfo>> objects = BuildStableObjectList(transaction);
            foreach (int objectIndex in objectIndexes.OrderBy(delegate(int value) { return value; }))
            {
                if (objectIndex < 0 || objectIndex >= objects.Count)
                {
                    throw new InvalidDataException("HS2PE 轨道引用了越界对象序号 " + objectIndex + "。");
                }

                OCIChar character = objects[objectIndex].Value as OCIChar;
                if (character == null)
                {
                    continue;
                }

                Transform root = character == null || character.guideObject == null
                    ? null
                    : character.guideObject.transformTarget;
                Component controller = root == null
                    ? null
                    : root.GetComponent(_hspeCharaPoseControllerType);
                object target = controller == null ? null : _hspeTargetField.GetValue(controller);
                if (character == null || controller == null || target == null)
                {
                    throw new InvalidDataException(
                        "HS2PE 轨道对象 " + objects[objectIndex].Key + " 的当前控制器尚未建立。");
                }

                _hspeRefreshFkBonesMethod.Invoke(target, null);
            }
        }

        private static void AppendAbmxFingerprint(OCIChar character, Transform root,
            ref int hash, ref bool ready)
        {
            if (_abmxBoneControllerType == null)
            {
                return;
            }

            Component controller = character.charInfo.GetComponent(_abmxBoneControllerType);
            hash = hash * 31 + UnityInstanceId(controller);
            if (controller == null)
            {
                ready = false;
                return;
            }

            object boneSearcher = _abmxBoneSearcherProperty.GetValue(controller, null);
            hash = hash * 31 + RuntimeIdentity(boneSearcher);
            object baselineKnown = _abmxBaselineKnownField.GetValue(controller);
            bool needsFullRefresh = (bool)_abmxNeedsFullRefreshProperty.GetValue(controller, null);
            bool needsBaselineUpdate = (bool)_abmxNeedsBaselineUpdateProperty.GetValue(controller, null);
            object previousAnimSpeed = _abmxPreviousAnimSpeedField.GetValue(controller);
            object partialTargets = _abmxPartialBaselineTargetsField.GetValue(controller);
            int partialTargetCount = GetCollectionCount(partialTargets);
            hash = hash * 31 + (baselineKnown is bool && (bool)baselineKnown ? 1 : 0);
            hash = hash * 31 + (needsFullRefresh ? 1 : 0);
            hash = hash * 31 + (needsBaselineUpdate ? 1 : 0);
            hash = hash * 31 + (previousAnimSpeed == null ? 0 : 1);
            hash = hash * 31 + partialTargetCount;
            if (boneSearcher == null ||
                !ReferenceEquals(_abmxBoneSearcherControlField.GetValue(boneSearcher),
                    character.charInfo) ||
                !(baselineKnown is bool) || !(bool)baselineKnown || needsFullRefresh ||
                needsBaselineUpdate || previousAnimSpeed != null || partialTargetCount > 0)
            {
                ready = false;
            }

            IEnumerable modifierEnumerable = _abmxGetAllModifiersMethod.Invoke(controller, null) as IEnumerable;
            List<object> modifiers = modifierEnumerable == null
                ? new List<object>()
                : modifierEnumerable.Cast<object>().ToList();
            modifiers.Sort(delegate(object left, object right)
            {
                int leftLocation = Convert.ToInt32(_abmxModifierLocationProperty.GetValue(left, null));
                int rightLocation = Convert.ToInt32(_abmxModifierLocationProperty.GetValue(right, null));
                int byLocation = leftLocation.CompareTo(rightLocation);
                if (byLocation != 0)
                {
                    return byLocation;
                }

                return string.CompareOrdinal(
                    _abmxModifierNameProperty.GetValue(left, null) as string ?? string.Empty,
                    _abmxModifierNameProperty.GetValue(right, null) as string ?? string.Empty);
            });
            hash = hash * 31 + modifiers.Count;
            for (int i = 0; i < modifiers.Count; i++)
            {
                object modifier = modifiers[i];
                string name = _abmxModifierNameProperty.GetValue(modifier, null) as string ?? string.Empty;
                int location = Convert.ToInt32(_abmxModifierLocationProperty.GetValue(modifier, null));
                Transform transform = _abmxModifierTransformProperty.GetValue(modifier, null) as Transform;
                bool hasBaseline = (bool)_abmxModifierHasBaselineField.GetValue(modifier);
                hash = hash * 31 + location;
                hash = hash * 31 + name.GetHashCode();
                hash = hash * 31 + UnityInstanceId(transform);
                hash = hash * 31 + (hasBaseline ? 1 : 0);
                if (transform != null && (!hasBaseline || !IsTransformUnderRoot(transform, root)))
                {
                    ready = false;
                }
            }
        }

        private static int GetCollectionCount(object value)
        {
            if (value == null)
            {
                return -1;
            }

            ICollection collection = value as ICollection;
            if (collection != null)
            {
                return collection.Count;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable == null)
            {
                return int.MaxValue;
            }

            int count = 0;
            foreach (object ignored in enumerable)
            {
                count++;
            }

            return count;
        }

        private static void AppendHsPeFingerprint(OCIChar character, Transform root,
            ref int hash, ref bool ready)
        {
            if (_hspeCharaPoseControllerType == null)
            {
                ready = false;
                return;
            }

            Component controller = root.GetComponent(_hspeCharaPoseControllerType);
            hash = hash * 31 + UnityInstanceId(controller);
            if (controller == null)
            {
                ready = false;
                return;
            }

            object target = _hspeTargetField.GetValue(controller);
            object bonesEditor = _hspeBonesEditorField.GetValue(controller);
            object dynamicBonesEditor = _hspeDynamicBonesEditorField.GetValue(controller);
            object blendShapesEditor = _hspeBlendShapesEditorField.GetValue(controller);
            object collidersEditor = _hspeCollidersEditorField.GetValue(controller);
            object ikEditor = _hspeIkEditorField.GetValue(controller);
            object clothesEditor = _hspeClothesTransformEditorField.GetValue(controller);
            object boobsEditor = _hspeBoobsEditorField.GetValue(controller);
            object body = _hspeBodyField.GetValue(controller);
            object[] identities =
            {
                target, bonesEditor, dynamicBonesEditor, blendShapesEditor, collidersEditor,
                ikEditor, clothesEditor, boobsEditor, body
            };
            for (int i = 0; i < identities.Length; i++)
            {
                hash = hash * 31 + RuntimeIdentity(identities[i]);
                if (identities[i] == null)
                {
                    ready = false;
                }
            }

            if (target == null)
            {
                return;
            }

            ObjectCtrlInfo targetOci = _hspeTargetOciField.GetValue(target) as ObjectCtrlInfo;
            OCIChar targetCharacter = _hspeTargetOciCharField.GetValue(target) as OCIChar;
            bool isFemale = (bool)_hspeTargetIsFemaleField.GetValue(target);
            hash = hash * 31 + RuntimeIdentity(targetOci);
            hash = hash * 31 + RuntimeIdentity(targetCharacter);
            hash = hash * 31 + (isFemale ? 1 : 0);
            if (!ReferenceEquals(targetOci, character) || !ReferenceEquals(targetCharacter, character) ||
                isFemale != (character.oiCharInfo.sex == 1) || !ReferenceEquals(body, character.finalIK))
            {
                ready = false;
            }

            IDictionary fkObjects = _hspeTargetFkObjectsField.GetValue(target) as IDictionary;
            if (!HsPeFkObjectsMatch(fkObjects, character))
            {
                _hspeRefreshFkBonesMethod.Invoke(target, null);
                fkObjects = _hspeTargetFkObjectsField.GetValue(target) as IDictionary;
            }
            int validBoneCount = character.listBones == null
                ? 0
                : character.listBones.Count(delegate(OCIChar.BoneInfo bone)
                {
                    return bone != null && bone.guideObject != null &&
                           bone.guideObject.transformTarget != null;
                });
            hash = hash * 31 + (fkObjects == null ? -1 : fkObjects.Count);
            if (fkObjects == null || fkObjects.Count != validBoneCount)
            {
                ready = false;
            }
            else
            {
                for (int i = 0; i < character.listBones.Count; i++)
                {
                    OCIChar.BoneInfo bone = character.listBones[i];
                    if (bone == null || bone.guideObject == null ||
                        bone.guideObject.transformTarget == null)
                    {
                        continue;
                    }

                    GameObject key = bone.guideObject.transformTarget.gameObject;
                    hash = hash * 31 + UnityInstanceId(key);
                    if (!fkObjects.Contains(key) || !ReferenceEquals(fkObjects[key], bone))
                    {
                        ready = false;
                    }
                }
            }

            Transform[] cachedBones =
            {
                _hspeSiriDamLField.GetValue(controller) as Transform,
                _hspeSiriDamRField.GetValue(controller) as Transform,
                _hspeKosiField.GetValue(controller) as Transform,
                _hspeLeftFoot2Field.GetValue(controller) as Transform,
                _hspeRightFoot2Field.GetValue(controller) as Transform
            };
            for (int i = 0; i < cachedBones.Length; i++)
            {
                hash = hash * 31 + UnityInstanceId(cachedBones[i]);
                if (!IsTransformUnderRoot(cachedBones[i], root))
                {
                    ready = false;
                }
            }

            bool dynamicBusy = dynamicBonesEditor != null &&
                               (bool)_hspeDynamicBonesBusyField.GetValue(dynamicBonesEditor);
            bool blendBusy = blendShapesEditor != null &&
                             (bool)_hspeBlendShapesBusyField.GetValue(blendShapesEditor);
            hash = hash * 31 + (dynamicBusy ? 1 : 0);
            hash = hash * 31 + (blendBusy ? 1 : 0);
            if (dynamicBusy || blendBusy)
            {
                ready = false;
            }
        }

        private static bool HsPeFkObjectsMatch(IDictionary fkObjects, OCIChar character)
        {
            if (fkObjects == null || character == null || character.listBones == null)
            {
                return false;
            }

            int validCount = 0;
            for (int i = 0; i < character.listBones.Count; i++)
            {
                OCIChar.BoneInfo bone = character.listBones[i];
                if (bone == null || bone.guideObject == null ||
                    bone.guideObject.transformTarget == null)
                {
                    continue;
                }

                validCount++;
                GameObject key = bone.guideObject.transformTarget.gameObject;
                if (!fkObjects.Contains(key) || !ReferenceEquals(fkObjects[key], bone))
                {
                    return false;
                }
            }

            return fkObjects.Count == validCount;
        }

        private static void AppendTimelinePathFingerprint(Transaction transaction,
            IList<KeyValuePair<int, ObjectCtrlInfo>> objects, IDictionary guideRegistry,
            bool nonTimelineGraphReady, ref int hash, ref bool ready)
        {
            XmlNodeList tracks = transaction.DeferredTimelineNode == null
                ? null
                : transaction.DeferredTimelineNode.SelectNodes(".//interpolable");
            if (tracks == null)
            {
                return;
            }

            foreach (XmlNode track in tracks)
            {
                XmlAttributeCollection attributes = track.Attributes;
                if (attributes == null || attributes["owner"] == null || attributes["id"] == null)
                {
                    continue;
                }

                string owner = attributes["owner"].Value ?? string.Empty;
                string id = attributes["id"].Value ?? string.Empty;
                bool hsPeBone = IsHs2PeBoneTrack(owner, id);
                bool builtInGuide = IsBuiltInGuideObjectTrack(owner, id);
                if (!hsPeBone && !builtInGuide)
                {
                    continue;
                }

                int objectIndex;
                if (attributes["objectIndex"] == null ||
                    !int.TryParse(attributes["objectIndex"].Value, out objectIndex) ||
                    objectIndex < 0 || objectIndex >= objects.Count)
                {
                    hash = hash * 31 - 1;
                    ready = false;
                    if (nonTimelineGraphReady &&
                        string.IsNullOrEmpty(transaction.BoneGraphPermanentError))
                    {
                        transaction.BoneGraphPermanentError = string.Format(
                            "Timeline {0}/{1} 引用了无效对象序号；该引用不是异步加载，已停止等待。",
                            owner, id);
                    }
                    continue;
                }

                XmlAttribute pathAttribute = hsPeBone
                    ? attributes["parameter"]
                    : attributes["guideObjectPath"];
                ObjectCtrlInfo oci = objects[objectIndex].Value;
                Transform root = oci == null || oci.guideObject == null
                    ? null
                    : oci.guideObject.transformTarget;
                string path = pathAttribute == null ? string.Empty : pathAttribute.Value ?? string.Empty;
                // Timeline stores an empty guideObjectPath for the OCI root itself. HS2PE bone
                // tracks, in contrast, require an actual relative bone path.
                Transform target = root == null
                    ? null
                    : string.IsNullOrEmpty(path)
                        ? builtInGuide ? root : null
                        : root.Find(path);
                if (target == null && root != null && !transaction.LoadingRecovery &&
                    transaction.Keys.Contains(objects[objectIndex].Key))
                {
                    string mappedPath;
                    if (TryMapRelativeTransformPath(root, path, out mappedPath))
                    {
                        path = mappedPath;
                        target = root.Find(path);
                    }
                }
                hash = hash * 31 + objectIndex;
                hash = hash * 31 + path.GetHashCode();
                hash = hash * 31 + UnityInstanceId(target);
                if (target == null)
                {
                    ready = false;
                    if (nonTimelineGraphReady &&
                        string.IsNullOrEmpty(transaction.BoneGraphPermanentError))
                    {
                        transaction.BoneGraphPermanentError = string.Format(
                            "Timeline {0}/{1} 无法解析对象 {2} 的骨骼路径“{3}”；角色层级已经加载完成，该路径可能是男性专属骨骼或陈旧轨道，已停止等待。",
                            owner, id, objects[objectIndex].Key,
                            string.IsNullOrEmpty(path) ? "<空>" : path);
                    }
                    continue;
                }

                if (builtInGuide)
                {
                    GuideObject guide = guideRegistry.Contains(target)
                        ? guideRegistry[target] as GuideObject
                        : null;
                    hash = hash * 31 + UnityInstanceId(guide);
                    if (guide == null || !ReferenceEquals(guide.transformTarget, target))
                    {
                        ready = false;
                        if (nonTimelineGraphReady &&
                            string.IsNullOrEmpty(transaction.BoneGraphPermanentError))
                        {
                            transaction.BoneGraphPermanentError = string.Format(
                                "Timeline {0}/{1} 的对象 {2} 路径“{3}”没有当前 GuideObject 注册；继续等待不会得到安全绑定，已停止等待。",
                                owner, id, objects[objectIndex].Key, path);
                        }
                    }
                }
                else
                {
                    Component poseController = _hspePoseControllerType == null || root == null
                        ? null
                        : root.GetComponent(_hspePoseControllerType);
                    object bonesEditor = poseController == null
                        ? null
                        : _hspeBonesEditorField.GetValue(poseController);
                    hash = hash * 31 + UnityInstanceId(poseController);
                    hash = hash * 31 + RuntimeIdentity(bonesEditor);
                    if (poseController == null || bonesEditor == null ||
                        !IsTransformUnderRoot(target, root))
                    {
                        ready = false;
                        if (nonTimelineGraphReady &&
                            string.IsNullOrEmpty(transaction.BoneGraphPermanentError))
                        {
                            transaction.BoneGraphPermanentError = string.Format(
                                "Timeline {0}/{1} 的对象 {2} 尚无可用的当前 HS2PE BonesEditor；骨架层级已稳定，已停止等待。",
                                owner, id, objects[objectIndex].Key);
                        }
                    }
                }
            }
        }

        private static int UnityInstanceId(UnityEngine.Object value)
        {
            return value == null ? 0 : value.GetInstanceID();
        }

        private static int GetSceneCharacterCount()
        {
            return Singleton<StudioCore>.IsInstance()
                ? Singleton<StudioCore>.Instance.dicObjectCtrl.Values.Count(
                    delegate(ObjectCtrlInfo value) { return value is OCIChar; })
                : 0;
        }

        private BoneMigrationReport SanitizeAndReapplyBones(Transaction transaction)
        {
            BoneMigrationReport report = new BoneMigrationReport();
            for (int keyIndex = 0; keyIndex < transaction.Keys.Count; keyIndex++)
            {
                int key = transaction.Keys[keyIndex];
                OCICharFemale target = StudioCore.GetCtrlInfo(key) as OCICharFemale;
                BoneSnapshot source = transaction.Bones[key];
                Dictionary<int, int> targetCounts = target.listBones
                    .GroupBy(delegate(OCIChar.BoneInfo value) { return value.boneID; })
                    .ToDictionary(delegate(IGrouping<int, OCIChar.BoneInfo> value) { return value.Key; },
                        delegate(IGrouping<int, OCIChar.BoneInfo> value) { return value.Count(); });
                HashSet<int> runtimeIds = new HashSet<int>();

                for (int i = 0; i < target.listBones.Count; i++)
                {
                    OCIChar.BoneInfo targetBone = target.listBones[i];
                    if (targetBone == null)
                    {
                        continue;
                    }

                    runtimeIds.Add(targetBone.boneID);
                    if (targetBone.boneInfo == null || targetBone.guideObject == null ||
                        targetBone.guideObject.transformTarget == null)
                    {
                        continue;
                    }

                    BoneSignature sourceBone;
                    bool safe = source.ById.TryGetValue(targetBone.boneID, out sourceBone) &&
                                sourceBone.Unique && targetCounts[targetBone.boneID] == 1 &&
                                IsSafeBoneMatch(targetBone, sourceBone);

                    if (safe)
                    {
                        targetBone.boneInfo.changeAmount.rot = sourceBone.Rotation;
                        report.Preserved++;
                    }
                    else
                    {
                        targetBone.boneInfo.changeAmount.rot = Vector3.zero;
                        report.Reset++;
                    }
                }

                int[] staleIds = target.oiCharInfo.bones.Keys
                    .Where(delegate(int id) { return !runtimeIds.Contains(id); })
                    .ToArray();
                for (int i = 0; i < staleIds.Length; i++)
                {
                    OIBoneInfo stale = target.oiCharInfo.bones[staleIds[i]];
                    target.oiCharInfo.bones.Remove(staleIds[i]);
                    StudioCore.DeleteChangeAmount(stale.dicKey);
                    StudioCore.DeleteIndex(stale.dicKey);
                    report.Pruned++;
                }
            }

            return report;
        }

        private bool IsSafeBoneMatch(OCIChar.BoneInfo target, BoneSignature source)
        {
            if (target.boneID == 67 || target.boneID == 68 || target.boneID == 69 ||
                target.guideObject == null || target.guideObject.transformTarget == null ||
                !string.Equals(target.guideObject.transformTarget.name, source.Name, StringComparison.Ordinal) ||
                target.boneGroup != source.Group)
            {
                return false;
            }

            if (!_strictModBoneMapping.Value)
            {
                return true;
            }

            return target.boneWeight == source.BoneWeight &&
                   string.Equals(BuildParentSuffix(target.guideObject.transformTarget, 4),
                       source.ParentSuffix, StringComparison.Ordinal);
        }

        private static string BuildParentSuffix(Transform transform, int depth)
        {
            List<string> names = new List<string>();
            Transform current = transform;
            while (current != null && names.Count < depth)
            {
                names.Add(current.name);
                current = current.parent;
            }

            names.Reverse();
            return string.Join("/", names.ToArray());
        }

        private static bool ValidateConvertedCharacters(Transaction transaction, out string error)
        {
            for (int i = 0; i < transaction.Keys.Count; i++)
            {
                int key = transaction.Keys[i];
                OCICharFemale female = StudioCore.GetCtrlInfo(key) as OCICharFemale;
                if (female == null || female.oiCharInfo == null || female.oiCharInfo.sex != 1 ||
                    female.charInfo == null || female.charInfo.sex != 1)
                {
                    error = "对象 ID " + key + " 未重建为 OCICharFemale。";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static void SelectFirstConvertedCharacter(Transaction transaction)
        {
            if (transaction.Keys.Count == 0 || !Singleton<StudioCore>.IsInstance())
            {
                return;
            }

            OCIChar character = StudioCore.GetCtrlInfo(transaction.Keys[0]) as OCIChar;
            if (character != null && character.treeNodeObject != null)
            {
                Singleton<StudioCore>.Instance.treeNodeCtrl.SelectSingle(character.treeNodeObject, true);
            }
        }

        private IEnumerator RollBack(Transaction transaction, string reason)
        {
            _log.LogError(reason);
            SetStatus(reason + "；正在自动恢复转换前场景……", 60f);

            Exception rollbackError = null;
            BeginConstraintLoadTracking(transaction, true);
            _activeTransaction = transaction;
            rollbackError = ClearExtensionRuntimeForRollback();
            if (rollbackError != null)
            {
                _log.LogError(rollbackError.Message);
            }

            IEnumerator rollbackRoutine = null;
            if (rollbackError == null)
            {
                try
                {
                    rollbackRoutine = Singleton<StudioCore>.Instance.LoadSceneCoroutine(
                        transaction.RecoveryPath);
                }
                catch (Exception error)
                {
                    rollbackError = error;
                }
            }

            if (rollbackError == null)
            {
                yield return DrainCoroutine(rollbackRoutine,
                    delegate(Exception error) { rollbackError = error; });
            }

            if (rollbackError == null)
            {
                Exception settleError = null;
                SetStatus("恢复场景已载入，正在等待全场骨架稳定……", 60f);
                yield return DrainCoroutine(WaitForSceneBoneGraphs(transaction,
                        delegate(Exception error) { settleError = error; }),
                    delegate(Exception error) { settleError = error; });
                rollbackError = settleError;
            }

            if (rollbackError == null)
            {
                Exception rebindError = null;
                SetStatus("全场骨架已稳定，正在恢复 NodesConstraints 与 Timeline 目标……", 60f);
                yield return DrainCoroutine(RebindExtensionsAfterSettle(transaction, false,
                        delegate(Exception error) { rebindError = error; }),
                    delegate(Exception error) { rebindError = error; });
                rollbackError = rebindError;
            }

            bool restored = rollbackError == null;
            for (int i = 0; restored && i < transaction.Keys.Count; i++)
            {
                OCIChar restoredCharacter = StudioCore.GetCtrlInfo(transaction.Keys[i]) as OCIChar;
                restored = restoredCharacter != null && restoredCharacter.oiCharInfo != null &&
                           restoredCharacter.oiCharInfo.sex == 0;
            }

            if (!restored)
            {
                rollbackError = rollbackError ??
                                new InvalidDataException("恢复快照载入后，目标对象未恢复为男性角色。");
                ClearTimelineRuntimeBestEffort();
                ClearNodesConstraintsRuntimeBestEffort();
            }

            _activeTransaction = null;
            transaction.LoadingRecovery = false;

            _busy = false;
            if (restored)
            {
                SelectFirstConvertedCharacter(transaction);
                SetStatus("替换失败，但已自动恢复转换前场景。详情请查看 BepInEx 日志。", 12f);
                _log.LogWarning("自动回滚成功：" + transaction.RecoveryPath);
            }
            else
            {
                string published = PublishEmergencyRecovery(transaction.RecoveryPath);
                string rollbackMessage = "自动回滚失败。恢复快照已保留：" + published;
                SetStatus(rollbackMessage, 20f);
                _log.LogError(rollbackMessage + (rollbackError == null ? string.Empty : "\n" + rollbackError));
            }
        }

        private void HandleEmergencyTransactionFailure(Transaction transaction, string reason,
            Exception error)
        {
            ClearTimelineRuntimeBestEffort();
            ClearNodesConstraintsRuntimeBestEffort();
            _activeTransaction = null;
            _snapshotTransaction = null;
            _busy = false;

            string recoveryPath = transaction == null || string.IsNullOrEmpty(transaction.RecoveryPath)
                ? string.Empty
                : PublishEmergencyRecovery(transaction.RecoveryPath);
            string message = string.IsNullOrEmpty(recoveryPath)
                ? reason
                : reason + " 恢复快照已保留：" + recoveryPath;
            SetStatus(message, 20f);
            _log.LogError(message + (error == null ? string.Empty : "\n" + error));
        }

        private static string PublishEmergencyRecovery(string source)
        {
            try
            {
                string directory = Path.Combine(Paths.GameRootPath, "UserData", "studio", "scene");
                Directory.CreateDirectory(directory);
                string destination = Path.Combine(directory,
                    "Codex_FemaleForMale_Recovery_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
                File.Copy(source, destination, true);
                return destination;
            }
            catch (Exception error)
            {
                _log.LogError("恢复快照复制到 Studio 场景目录失败：" + error);
                return source;
            }
        }

        private static IEnumerator DrainCoroutine(IEnumerator root, Action<Exception> onError)
        {
            Stack<IEnumerator> stack = new Stack<IEnumerator>();
            if (root != null)
            {
                stack.Push(root);
            }

            while (stack.Count > 0)
            {
                IEnumerator currentEnumerator = stack.Peek();
                bool moved;
                object current = null;
                try
                {
                    moved = currentEnumerator.MoveNext();
                    if (moved)
                    {
                        current = currentEnumerator.Current;
                    }
                }
                catch (Exception error)
                {
                    onError(error);
                    yield break;
                }

                if (!moved)
                {
                    stack.Pop();
                    continue;
                }

                IEnumerator nested = current as IEnumerator;
                if (nested != null && !(current is CustomYieldInstruction))
                {
                    stack.Push(nested);
                }
                else
                {
                    yield return current;
                }
            }
        }

        private sealed class Transaction
        {
            public readonly List<int> Keys = new List<int>();
            public readonly List<InfoMutation> Mutations = new List<InfoMutation>();
            public readonly Dictionary<int, BoneSnapshot> Bones = new Dictionary<int, BoneSnapshot>();
            public string CardPath;
            public string RecoveryPath;
            public string ConvertedPath;
            public int ConstraintPathsRemapped;
            public readonly HashSet<int> ExpectedConstraintIds = new HashSet<int>();
            public readonly HashSet<int> FailedConstraintIds = new HashSet<int>();
            public readonly Dictionary<int, object> LoadedConstraintObjects =
                new Dictionary<int, object>();
            public object NodesConstraintsInstance;
            public int UnidentifiedConstraints;
            public string ConstraintAdapterError;
            public int SavedConstraintCount = -1;
            public bool ConstraintSaveObserved;
            public bool ConstraintSaveCompleted;
            public string ConstraintSaveError;
            public bool TimelineSaveObserved;
            public bool TimelineSaveCompleted;
            public string TimelineSaveError;
            public int TimelineRuntimeTrackCount = -1;
            public int TimelineSnapshotTrackCount = -1;
            public bool ConstraintLoadObserved;
            public bool ConstraintLoadCompleted;
            public bool LoadingRecovery;
            public bool ConstraintSceneLoadDeferred;
            public object DeferredNodesConstraintsInstance;
            public string DeferredNodesConstraintsPath;
            public XmlNode DeferredNodesConstraintsNode;
            public bool TimelineSceneLoadDeferred;
            public bool TimelineLoadObserved;
            public bool TimelineLoadCompleted;
            public string TimelineAdapterError;
            public object DeferredTimelineInstance;
            public string DeferredTimelinePath;
            public XmlNode DeferredTimelineNode;
            public int ExpectedTimelineTrackCount = -1;
            public int LoadedTimelineTrackCount;
            public int InvalidTimelineBindingCount;
            public int TimelineBindingFingerprint = int.MinValue;
            public int SettledGraphFingerprint = int.MinValue;
            public string BoneGraphPermanentError;
            public int SceneLoadStartFrame;
            public int ExpectedCharacterCount;
            public readonly List<int> ExpectedObjectKeys = new List<int>();
            public int TimelinePathsRemapped;

            public int UnresolvedConstraints
            {
                get { return FailedConstraintIds.Count + UnidentifiedConstraints; }
            }
        }

        private sealed class InfoMutation
        {
            public OICharInfo Info;
            public int OriginalSex;
            public ChaFileControl OriginalFile;
            public ChaFileControl FemaleFile;
            public readonly Dictionary<int, OIBoneInfo> RemovedMaleBones =
                new Dictionary<int, OIBoneInfo>();
        }

        private sealed class BoneSnapshot
        {
            public readonly Dictionary<int, BoneSignature> ById = new Dictionary<int, BoneSignature>();
        }

        private struct BoneSignature
        {
            public bool Unique;
            public string Name;
            public string ParentSuffix;
            public OIBoneInfo.BoneGroup Group;
            public Vector3 Rotation;
            public bool BoneWeight;
        }

        private struct BoneMigrationReport
        {
            public int Preserved;
            public int Reset;
            public int Pruned;
        }

        [HarmonyPatch(typeof(CharaList), "OnSelectChara")]
        private static class CharaListSelectCardPatch
        {
            private static void Postfix(CharaList __instance)
            {
                RefreshFemaleChangeButton(__instance);
            }
        }

        [HarmonyPatch(typeof(CharaList), "OnSelect")]
        private static class CharaListObjectSelectPatch
        {
            private static void Postfix(CharaList __instance)
            {
                RefreshFemaleChangeButton(__instance);
            }
        }

        [HarmonyPatch(typeof(CharaList), "OnDeselect")]
        private static class CharaListObjectDeselectPatch
        {
            private static void Postfix(CharaList __instance)
            {
                RefreshFemaleChangeButton(__instance);
            }
        }

        [HarmonyPatch(typeof(CharaList), "OnDelete")]
        private static class CharaListObjectDeletePatch
        {
            private static void Postfix(CharaList __instance)
            {
                RefreshFemaleChangeButton(__instance);
            }
        }

        [HarmonyPatch(typeof(CharaList), nameof(CharaList.ChangeCharaFemale))]
        private static class ChangeCharaFemalePatch
        {
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(CharaList __instance)
            {
                if (!Active)
                {
                    return true;
                }

                List<OCIChar> selected = GetSelectedCharacters();
                List<OCIChar> males = selected.Where(delegate(OCIChar value)
                {
                    return value.oiCharInfo != null && value.oiCharInfo.sex == 0;
                }).ToList();

                if (males.Count == 0)
                {
                    return true;
                }

                if (_instance._busy)
                {
                    _instance.SetStatus("上一次完整替换仍在进行，请等待场景重载完成。", 5f);
                    return false;
                }

                string path = GetSelectedFemaleCard(__instance);
                if (string.IsNullOrEmpty(path))
                {
                    _instance.SetStatus("请先在女性角色列表中选一张角色卡。", 6f);
                    return false;
                }

                if (selected.Count != males.Count)
                {
                    _log.LogWarning("本次混合选择中只转换男角色；已选女角色保持不变。");
                }

                int[] keys = males.Select(delegate(OCIChar value)
                {
                    return value.objectInfo.dicKey;
                }).ToArray();
                _instance.StartCoroutine(_instance.ReplaceSelectedMales(keys, path));
                return false;
            }
        }

        private static bool CharacterLoaderReplacePrefix(object __instance, string __0, int __1)
        {
            if (!Active || __1 != 1)
            {
                return true;
            }

            List<OCIChar> selected = GetSelectedCharacters();
            List<OCIChar> males = selected.Where(delegate(OCIChar value)
            {
                return value.oiCharInfo != null && value.oiCharInfo.sex == 0;
            }).ToList();
            if (males.Count == 0)
            {
                return true;
            }

            if (_instance._busy)
            {
                _instance.SetStatus("上一次完整替换仍在进行，请等待场景重载完成。", 5f);
                return false;
            }

            string path = __0 ?? string.Empty;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                _instance.SetStatus("人物卡预览插件传入的女性角色卡不存在，替换未开始。", 8f);
                _log.LogError("Character Loader 替换入口收到无效卡片路径：" + path);
                return false;
            }

            if (selected.Count != males.Count)
            {
                _log.LogWarning("本次混合选择中只转换男角色；已选女角色保持不变。");
            }

            int[] keys = males.Select(delegate(OCIChar value)
            {
                return value.objectInfo.dicKey;
            }).ToArray();
            _instance.StartCoroutine(_instance.ReplaceSelectedMales(keys, path));
            _instance.CloseCharacterLoaderIfConfigured(__instance);
            return false;
        }
    }
}
