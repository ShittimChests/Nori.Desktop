using SkiaSharp;

namespace Live2DCSharpSDK.App;

/// <summary>
/// 画像読み込み、管理を行うクラス。
/// </summary>
public class LAppTextureManager(LAppDelegate lapp)
{
    private readonly List<TextureInfo> _textures = [];

    /// <summary>
    /// 共享纹理的引用计数。
    ///
    /// 同一模型重载时, 候选模型会先按文件名复用旧模型的 TextureInfo, 之后旧模型才被
    /// 释放; 若 ReleaseTexture 无条件 Dispose, 新模型手里的 GL 纹理 id 会立刻悬垂。
    /// 这里按持有者计数, 只有最后一个持有者才真正删除纹理。键用引用相等。
    /// </summary>
    private readonly Dictionary<TextureInfo, int> _references = [];

    /// <summary>
    /// 画像読み込み
    /// </summary>
    /// <param name="fileName">読み込む画像ファイルパス名</param>
    /// <returns>画像情報。読み込み失敗時はNULLを返す</returns>
    public unsafe TextureInfo CreateTextureFromPngFile(LAppModel model, int index, string fileName)
    {
        //search loaded texture already.
        var item = _textures.FirstOrDefault(a => a.FileName == fileName);
        if (item != null)
        {
            _references[item] = _references.GetValueOrDefault(item) + 1;
            return item;
        }
        var info1 = SKBitmap.DecodeBounds(fileName);
        info1.ColorType = SKColorType.Rgba8888;
        using var image = SKBitmap.Decode(fileName, info1);

        // OpenGL用のテクスチャを生成する
        var info = lapp.CreateTexture(model, index, image.Width, image.Height, image.GetPixels());
        info.FileName = fileName;
        info.Width = image.Width;
        info.Index = index;
        info.Height = image.Height;

        _textures.Add(info);
        _references[info] = 1;

        return info;
    }

    /// <summary>
    /// 指定したテクスチャIDの画像を解放する
    ///
    /// 仍有其他模型持有该纹理时只减计数, 不删除 GL 资源。
    /// </summary>
    /// <param name="textureId">解放するテクスチャID</param>
    public void ReleaseTexture(TextureInfo info)
    {
        if (_references.TryGetValue(info, out int count) && count > 1)
        {
            _references[info] = count - 1;
            return;
        }
        _references.Remove(info);
        info.Dispose();
        _textures.Remove(info);
    }
}
