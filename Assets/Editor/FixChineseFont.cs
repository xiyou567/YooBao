using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// 一键修复中文字体。
///
/// 不修现有的 simhei SDF.asset，而是用 simhei.ttf 重新生成一个全新的字体资产，
/// 绕开旧资产可能存在的 guid / 图集问题。
///
/// 用法：菜单栏 Tools > 一键修复中文字体
/// 报告写到 font_report.txt，用完这个文件可以删掉。
/// </summary>
public static class FixChineseFont
{
    private const string TtfPath = "Assets/Fonts/simhei.ttf";
    private const string OldFontPath = "Assets/Fonts/simhei SDF.asset";
    // 注意：不能叫 "SimHei SDF.asset" —— Windows 大小写不敏感，会和旧资产 "simhei SDF.asset" 撞成同一个文件
    private const string NewFontPath = "Assets/Fonts/YooBot SimHei SDF.asset";
    private const string SettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
    private const string ReportPath = "D:/GameCreate/YooBao/font_report.txt";

    private const string SampleText = "你好柚宝我是玩家发发送";

    [MenuItem("Tools/一键修复中文字体")]
    public static void Fix()
    {
        StringBuilder log = new StringBuilder();

        try
        {
            // 1. 加载原始 ttf
            Font ttf = AssetDatabase.LoadAssetAtPath<Font>(TtfPath);
            log.AppendLine($"[1] 加载 {TtfPath} -> {(ttf == null ? "❌ 失败" : "✅ " + ttf.name)}");
            if (ttf == null) { Flush(log); return; }

            // 2. 旧字体资产是什么状况
            TMP_FontAsset old = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OldFontPath);
            if (old == null)
            {
                log.AppendLine($"[2] 旧资产 {OldFontPath} -> ❌ 加载为 null");
            }
            else
            {
                log.AppendLine($"[2] 旧资产 {OldFontPath}");
                log.AppendLine($"      name          = {old.name}");
                log.AppendLine($"      guid(路径取)  = {AssetDatabase.AssetPathToGUID(OldFontPath)}");
                log.AppendLine($"      sourceFontFile= {(old.sourceFontFile == null ? "❌ 空（这就是方块的原因：动态模式生不出新字形）" : "✅ " + old.sourceFontFile.name)}");
                log.AppendLine($"      atlasMode     = {old.atlasPopulationMode}");
                log.AppendLine($"      pointSize     = {old.faceInfo.pointSize}（正常应为 90 左右）");
                log.AppendLine($"      字符数        = {(old.characterTable == null ? -1 : old.characterTable.Count)}");
                log.AppendLine($"      图集页数      = {(old.atlasTextures == null ? -1 : old.atlasTextures.Length)}");
            }

            // 3. 用 simhei.ttf 重新生成一个干净的字体资产
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NewFontPath) != null)
            {
                AssetDatabase.DeleteAsset(NewFontPath);
                log.AppendLine($"[3] 已删除同名的旧 {NewFontPath}");
            }

            TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(
                ttf, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048,
                AtlasPopulationMode.Dynamic, true);

            if (font == null)
            {
                log.AppendLine("[3] ❌ CreateFontAsset 返回 null");
                Flush(log);
                return;
            }

            AssetDatabase.CreateAsset(font, NewFontPath);

            if (font.material != null)
            {
                font.material.name = font.name + " Material";
                AssetDatabase.AddObjectToAsset(font.material, font);
            }
            if (font.atlasTextures != null)
            {
                for (int i = 0; i < font.atlasTextures.Length; i++)
                {
                    if (font.atlasTextures[i] == null) continue;
                    font.atlasTextures[i].name = font.name + " Atlas " + i;
                    AssetDatabase.AddObjectToAsset(font.atlasTextures[i], font);
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(NewFontPath, ImportAssetOptions.ForceUpdate);

            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NewFontPath);
            log.AppendLine($"[3] ✅ 新资产 {NewFontPath}");
            log.AppendLine($"      guid = {AssetDatabase.AssetPathToGUID(NewFontPath)}");
            log.AppendLine($"      name = {font.name}");

            // 4. 试生成样本汉字 —— 这一步能验出源字体到底有没有汉字字形
            log.AppendLine($"[4] 新资产 sourceFontFile = {(font.sourceFontFile == null ? "❌ 空" : "✅ " + font.sourceFontFile.name)}");
            log.AppendLine($"      pointSize = {font.faceInfo.pointSize}（这次应该是 90）");
            log.AppendLine($"      动态模式  = {font.atlasPopulationMode}");

            string missing;
            bool ok = font.TryAddCharacters(SampleText, out missing);
            log.AppendLine($"[4] 试生成 \"{SampleText}\"");
            log.AppendLine($"      结果   = {(ok ? "✅ 全部成功" : "⚠️ 缺字: " + missing)}");
            log.AppendLine($"      字符数 = {(font.characterTable == null ? -1 : font.characterTable.Count)}");

            // 4b. 预热界面和提示词里会出现的字，避免首帧现生成卡一下
            string prewarm = "发送柚宝你好我是玩家请输入消息。，？！、：（）…—";
            font.TryAddCharacters(prewarm, out _);
            log.AppendLine($"[4b] 预热后字符数 = {(font.characterTable == null ? -1 : font.characterTable.Count)}");
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();

            // 5. 设为 TMP 全局默认字体
            TMP_Settings settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(SettingsPath);
            if (settings != null)
            {
                SerializedObject so = new SerializedObject(settings);
                SerializedProperty p = so.FindProperty("m_defaultFontAsset");
                if (p != null)
                {
                    p.objectReferenceValue = font;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(settings);
                    log.AppendLine("[5] ✅ TMP Settings 默认字体已更新");
                }
                else
                {
                    log.AppendLine("[5] ❌ 找不到 m_defaultFontAsset 字段");
                }
            }
            else
            {
                log.AppendLine($"[5] ❌ 加载不到 {SettingsPath}");
            }

            // 6. 场景里所有 TMP 组件（顺便记录它们改之前是什么）
            TMP_Text[] texts = Object.FindObjectsOfType<TMP_Text>(true);
            log.AppendLine($"[6] 场景 TMP 文本 {texts.Length} 个：");
            foreach (TMP_Text t in texts)
            {
                log.AppendLine($"      [{t.name}] 原字体 = {(t.font == null ? "❌ 空/悬空" : t.font.name)}");
            }
            foreach (TMP_Text t in texts)
            {
                t.font = font;
                EditorUtility.SetDirty(t);
            }

            foreach (TMP_InputField input in Object.FindObjectsOfType<TMP_InputField>(true))
            {
                SerializedObject so = new SerializedObject(input);
                SerializedProperty p = so.FindProperty("m_GlobalFontAsset");
                if (p != null)
                {
                    p.objectReferenceValue = font;
                    so.ApplyModifiedProperties();
                }
                EditorUtility.SetDirty(input);
            }
            log.AppendLine("      已全部指向新字体");

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkAllScenesDirty();
            EditorSceneManager.SaveOpenScenes();
            log.AppendLine("[7] ✅ 场景已保存");
        }
        catch (System.Exception e)
        {
            log.AppendLine("!! 异常：" + e);
        }

        Flush(log);
    }

    private static void Flush(StringBuilder log)
    {
        string text = log.ToString();
        Debug.Log(text);
        try { File.WriteAllText(ReportPath, text); }
        catch (System.Exception e) { Debug.LogError("写报告失败: " + e.Message); }
    }
}
