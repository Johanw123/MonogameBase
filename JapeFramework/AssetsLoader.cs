using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Content.Pipeline.Processors;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Audio;
using MonoGame.Framework.Content.Pipeline.Builder;
using System.Reflection;
using System.IO;
using Microsoft.Xna.Framework.Content.Pipeline.Audio;
using Microsoft.Xna.Framework.Media;
using Microsoft.Xna.Framework;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;
using static System.Net.Mime.MediaTypeNames;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;
using System.Text.Json;
using BracketHouse.FontExtension;
using System.Xml.Linq;
using Serilog;
using AsepriteDotNet.Aseprite;
using AsepriteDotNet.IO;

namespace AsyncContent
{
  /// <summary>
  /// An asset loader for MonoGame to import raw files (textures, fbx etc) without needing to build them upfront via the monogame content manager tool.
  /// In other words, use this class to load models and assets directly into MonoGame project.
  /// Author: Ronen Ness
  /// Date: 09/2021
  /// </summary>
  public class AssetsLoader
  {
    // graphics device
    GraphicsDevice _graphics;

    // context objects
    PipelineImporterContext _importContext;
    PipelineProcessorContext _processContext;

    // importers
    OpenAssetImporter _openImporter;
    EffectImporter _effectImporter;
    FontDescriptionImporter _fontImporter;
    Dictionary<string, ContentImporter<AudioContent>> _soundImporters = new Dictionary<string, ContentImporter<AudioContent>>();

    // processors
    ModelProcessor _modelProcessor;
    EffectProcessor _effectProcessor;
    FontDescriptionProcessor _fontProcessor;

    // loaded assets caches
    Dictionary<string, Object> _loadedAssets = new Dictionary<string, Object>();

    // default effect to assign to all meshes
    public BasicEffect DefaultEffect;

    /// <summary>
    /// Method to generate / return effect per mesh part (or null to use default).
    /// </summary>
    public EffectsGenerator EffectsGenerator;


    private bool m_loadAsIfPublish = false;

    /// <summary>
    /// Create the assets loader.
    /// </summary>
    public AssetsLoader(GraphicsDevice graphics)
    {
      _graphics = graphics;
      _openImporter = new OpenAssetImporter();
      _effectImporter = new EffectImporter();
      _soundImporters[".wav"] = new WavImporter();
      _soundImporters[".ogg"] = new OggImporter();
      _soundImporters[".wma"] = new WmaImporter();
      _soundImporters[".mp3"] = new Mp3Importer();
      _fontImporter = new FontDescriptionImporter();

      string projectDir = "_proj";
      string outputDir = "_output";
      string intermediateDir = "_inter";
      var pipelineManager = new PipelineManager(projectDir, outputDir, intermediateDir);

      _importContext = new PipelineImporterContext(pipelineManager, new PipelineBuildEvent());
      _processContext = new PipelineProcessorContext(pipelineManager, new PipelineBuildEvent());

      _modelProcessor = new ModelProcessor();
      _effectProcessor = new EffectProcessor();
      _fontProcessor = new FontDescriptionProcessor();

      DefaultEffect = new BasicEffect(_graphics);
    }

    /// <summary>
    /// Clear models cache.
    /// </summary>
    public void ClearCache()
    {
      _loadedAssets.Clear();
    }

    /// <summary>
    /// Validate given path, and attempt to return from cache.
    /// Will return true if found in cache, false otherwise (fromCache will return result).
    /// Will throw exceptions if path not found, or cache contains wrong type.
    /// </summary>
    bool ValidatePathAndGetCached<T>(string assetPath, out T fromCache) where T : class
    {
      // get from cache
      if (_loadedAssets.TryGetValue(assetPath, out object cached))
      {
        fromCache = cached as T;
        if (fromCache == null) { throw new InvalidOperationException($"Asset path found in cache, but has a wrong type! Expected type: '{(typeof(T)).Name}', found type: '{cached.GetType().Name}'."); }
        return true;
      }

      // make sure file exists
      if (!File.Exists(assetPath))
      {
        throw new FileNotFoundException($"{(typeof(T)).Name} asset file '{assetPath}' not found!", assetPath);
      }

      // not found in cache
      fromCache = null;
      return false;
    }

    /// <summary>
    /// LoadAsync a model from path.
    /// </summary>
    /// <param name="modelPath">Model file path.</param>
    /// <returns>MonoGame model.</returns>
    public Model LoadModel(string modelPath)
    {
      // validate path and get from cache
      if (ValidatePathAndGetCached(modelPath, out Model cached))
      {
        return cached;
      }

      // load model and convert to model content
      var node = _openImporter.Import(modelPath, _importContext);
      ModelContent modelContent = _modelProcessor.Process(node, _processContext);

      // sanity
      if (modelContent.Meshes.Count == 0)
      {
        throw new FormatException("Model file contains 0 meshes (could it be corrupted or unsupported type?)");
      }

      // extract bones
      var bones = new List<ModelBone>();
      foreach (var boneContent in modelContent.Bones)
      {
        var bone = new ModelBone
        {
          Transform = boneContent.Transform,
          Index = bones.Count,
          Name = boneContent.Name,
          ModelTransform = modelContent.Root.Transform
        };
        bones.Add(bone);
      }

      // resolve bones hirarchy
      for (var index = 0; index < bones.Count; ++index)
      {
        var bone = bones[index];
        var content = modelContent.Bones[index];
        if (content.Parent != null && content.Parent.Index != -1)
        {
          bone.Parent = bones[content.Parent.Index];
          bone.Parent.AddChild(bone);
        }
      }

      // extract meshes
      var meshes = new List<ModelMesh>();
      foreach (var meshContent in modelContent.Meshes)
      {
        // get params
        var name = meshContent.Name;
        var parentBoneIndex = meshContent.ParentBone.Index;
        var boundingSphere = meshContent.BoundingSphere;
        var meshTag = meshContent.Tag;

        // extract parts
        var parts = new List<ModelMeshPart>();
        foreach (var partContent in meshContent.MeshParts)
        {
          // build index buffer
          IndexBuffer indexBuffer = new IndexBuffer(_graphics, IndexElementSize.ThirtyTwoBits, partContent.IndexBuffer.Count, BufferUsage.WriteOnly);
          {
            Int32[] data = new Int32[partContent.IndexBuffer.Count];
            partContent.IndexBuffer.CopyTo(data, 0);
            indexBuffer.SetData(data);
          }

          // build vertex buffer
          var vbDeclareContent = partContent.VertexBuffer.VertexDeclaration;
          List<VertexElement> elements = new List<VertexElement>();
          foreach (var declareContentElem in vbDeclareContent.VertexElements)
          {
            elements.Add(new VertexElement(declareContentElem.Offset, declareContentElem.VertexElementFormat, declareContentElem.VertexElementUsage, declareContentElem.UsageIndex));
          }
          var vbDeclare = new VertexDeclaration(elements.ToArray());
          VertexBuffer vertexBuffer = new VertexBuffer(_graphics, vbDeclare, partContent.NumVertices, BufferUsage.WriteOnly);
          {
            vertexBuffer.SetData(partContent.VertexBuffer.VertexData);
          }

          // create and add part
#pragma warning disable CS0618 // Type or member is obsolete
          ModelMeshPart part = new ModelMeshPart()
          {
            VertexOffset = partContent.VertexOffset,
            NumVertices = partContent.NumVertices,
            PrimitiveCount = partContent.PrimitiveCount,
            StartIndex = partContent.StartIndex,
            Tag = partContent.Tag,
            IndexBuffer = indexBuffer,
            VertexBuffer = vertexBuffer
          };
#pragma warning restore CS0618 // Type or member is obsolete
          parts.Add(part);
        }

        // create and add mesh to meshes list
        var mesh = new ModelMesh(_graphics, parts)
        {
          Name = name,
          BoundingSphere = boundingSphere,
          Tag = meshTag,
        };
        meshes.Add(mesh);

        // set parts effect (note: this must come *after* we add parts to the mesh otherwise we get exception).
        foreach (var part in parts)
        {
          var effect = EffectsGenerator != null ? EffectsGenerator(modelPath, modelContent, part) ?? DefaultEffect : DefaultEffect;
          part.Effect = effect;
        }

        // add to parent bone
        if (parentBoneIndex != -1)
        {
          mesh.ParentBone = bones[parentBoneIndex];
          mesh.ParentBone.AddMesh(mesh);
        }
      }

      // create model
      var model = new Model(_graphics, bones, meshes);
      model.Root = bones[modelContent.Root.Index];
      model.Tag = modelContent.Tag;

      // we need to call BuildHierarchy() but its internal, so we use reflection to access it ¯\_(ツ)_/¯
      var methods = model.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic);
      var BuildHierarchy = methods.Where(x => x.Name == "BuildHierarchy" && x.GetParameters().Length == 0).First();
      BuildHierarchy.Invoke(model, null);

      // add to cache and return
      _loadedAssets[modelPath] = model;
      return model;
    }


    private Effect GenerateEffect(string root, string effectFile, bool forceReload)
    {
      //TODO: move files to Failed to load specified font file.GeneratedShaders in Content
      //At least for making a release build

      bool isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
      bool isMac = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
      bool isArm = RuntimeInformation.OSArchitecture == Architecture.Arm64;

      // TODO: check timestamp on .mgfx file, is it older than the .fx file? then recompile it
      if (Path.GetExtension(effectFile) == ".fx")
      {
        var projPath = PathHelper.FindProjectDirectory();

        var relativeEffectPath = Path.GetRelativePath(projPath, effectFile);
        var absEffectPath = Path.Combine(projPath, relativeEffectPath);

        var p = Path.GetDirectoryName(effectFile);
        var pp = Path.Combine(projPath, p, "GeneratedShaders");
        var name = Path.GetFileNameWithoutExtension(effectFile);

        var outputAbsFilePath = Path.Combine(pp, name + ".mgfx");
        var outputRelativeFilePath = Path.GetRelativePath(projPath, outputAbsFilePath);

        var outputPathWithoutFilename = Path.GetDirectoryName(outputAbsFilePath);

        if (!Path.Exists(outputPathWithoutFilename))
        {
          Directory.CreateDirectory(outputPathWithoutFilename);
        }

        if (!forceReload && File.Exists(outputAbsFilePath))
          return LoadCompiledEffect(outputAbsFilePath, false);

        if (isArm)
        {
          string mgfxcPath = "~/Dev/mgfxc/mgfxc.exe";
          ProcessHelper.RunExe(mgfxcPath, $"{absEffectPath} {outputAbsFilePath} /Profile:OpenGL");
        }
        else
        {
          ProcessHelper.RunCommand("mgfxc",
            isLinux ? $"{relativeEffectPath} {outputRelativeFilePath}" : $"{absEffectPath} {outputAbsFilePath}");
        }

        return LoadCompiledEffect(outputAbsFilePath, forceReload);
      }

      return null;
    }

    /// <summary>
    /// LoadAsync an effect from path.
    /// Note: requires the mgfxc dll to work.
    /// </summary>
    /// <param name="effectFile">Effect file path.</param>
    /// <returns>MonoGame Effect.</returns>
    public Effect LoadEffect(string effectFile, bool forceReload)
    {
      // validate path and get from cache
      if (!forceReload && ValidatePathAndGetCached(effectFile, out Effect cached))
      {
        return cached;
      }

      if (Path.GetExtension(effectFile) == ".mgfx")
      {
        return LoadCompiledEffect(effectFile, forceReload);
      }

      var root = PathHelper.FindSolutionDirectory();

      var stackTrace = new StackTrace(false);
      var isAot = stackTrace.GetFrame(0)?.GetMethod() is null;

      // Console.WriteLine($"isAot: " + isAot);
      // Console.WriteLine($"root: " + root);

      if (root == null || m_loadAsIfPublish || isAot)
      {
        root = Directory.GetCurrentDirectory();
        var fpath = Path.Combine(root, Path.GetDirectoryName(effectFile));
        var spath = Path.Combine(fpath, "GeneratedShaders");
        var name = Path.GetFileNameWithoutExtension(effectFile);

        var effectPath = Path.Combine(spath, $"{name}.mgfx");

        Log.Debug("Loading effect: " + effectPath);

        return LoadCompiledEffect(effectPath, forceReload);
      }

      return GenerateEffect(root, effectFile, forceReload);
    }


    /// <summary>
    /// LoadAsync a compiled effect (.fx file that was built via mgfxc) from path.
    /// To build .fx files into compiled shaders:
    /// 1. run `dotnet tool install -g dotnet-mgfxc` to get the building tool.
    /// 2. Run `mgfxc <SourceFile> <OutputFile> /Profile:OpenGL` to build the shader (you can change the Profile param for DX or PS, check out --help).
    /// </summary>
    /// <param name="effectFile">Effect file path.</param>
    /// <returns>MonoGame Effect.</returns>
    public Effect LoadCompiledEffect(string effectFile, bool forceReload)
    {
      Log.Debug("Loading effect from file: " + effectFile);

      // validate path and get from cache
      if (!forceReload && ValidatePathAndGetCached(effectFile, out Effect cached))
      {
        return cached;
      }

      if (!File.Exists(effectFile))
      {
        Log.Error("Failed to load compiled shader (file doesnt Exists): " + effectFile);
        return null;
      }

      byte[] bytecode = File.ReadAllBytes(effectFile);

      var effect = new Effect(_graphics, bytecode, 0, bytecode.Length);
      _loadedAssets[effectFile] = effect;

      return effect;
    }

    /// <summary>
    /// LoadAsync a sound effect from file.
    /// </summary>
    /// <param name="soundFile">Sound effect file path.</param>
    /// <returns>MonoGame SoundEffect.</returns>
    public SoundEffect LoadSound(string soundFile, bool forceReload)
    {
      // validate path and get from cache
      if (!forceReload && ValidatePathAndGetCached(soundFile, out SoundEffect cached))
      {
        return cached;
      }

      // import audio
      var extension = Path.GetExtension(soundFile).ToLower();
      if (!_soundImporters.ContainsKey(extension))
      {
        throw new InvalidContentException($"Invalid sound file type '{extension}'. Can only load sound files of types: '{string.Join(',', _soundImporters.Keys)}'.");
      }
      var audioContent = _soundImporters[extension].Import(soundFile, _importContext);

      // create sound and return
      byte[] data = new byte[audioContent.Data.Count];
      audioContent.Data.CopyTo(data, 0);
      var sound = new SoundEffect(data, 0, data.Length, audioContent.Format.SampleRate, audioContent.Format.ChannelCount == 1 ? AudioChannels.Mono : AudioChannels.Stereo, audioContent.LoopStart, audioContent.LoopLength);

      // add to cache and return
      _loadedAssets[soundFile] = sound;
      return sound;
    }

    /// <summary>
    /// LoadAsync a spritefont from file.
    /// </summary>
    /// <param name="fontFile">Spritefont file path (xml file describing the spritefont).</param>
    /// <returns>MonoGame SpriteFont.</returns>
    public SpriteFont LoadSpriteFont(string fontFile)
    {
      // validate path and get from cache
      if (ValidatePathAndGetCached(fontFile, out SpriteFont cached))
      {
        return cached;
      }

      // import spritefont xml file
      var fontDescription = _fontImporter.Import(fontFile, _importContext);
      var spriteFontContent = _fontProcessor.Process(fontDescription, _processContext);

      // create spritefont
      var textureContent = spriteFontContent.Texture.Mipmaps[0];
      textureContent.TryGetFormat(out SurfaceFormat format);
      Texture2D texture = new Texture2D(_graphics, textureContent.Width, textureContent.Height, false, format);
      texture.SetData(textureContent.GetPixelData());
      List<Rectangle> glyphBounds = spriteFontContent.Glyphs;
      List<Rectangle> cropping = spriteFontContent.Cropping;
      List<char> characters = spriteFontContent.CharacterMap;
      int lineSpacing = spriteFontContent.VerticalLineSpacing;
      float spacing = spriteFontContent.HorizontalSpacing;
      List<Vector3> kerning = spriteFontContent.Kerning;
      char? defaultCharacter = spriteFontContent.DefaultCharacter;
      var sf = new SpriteFont(texture, glyphBounds, cropping, characters, lineSpacing, spacing, kerning, defaultCharacter);

      // add to cache and return
      _loadedAssets[fontFile] = sf;
      return sf;
    }

    private FieldFont GenerateFieldFont(string root, string fontPath, bool forceReload)
    {
      //TODO: maybe a better way
      var projectPath = PathHelper.FindProjectDirectory();

      var p = Path.GetDirectoryName(fontPath);
      //var pp = Path.Combine(root, outPath, p, "GeneratedFonts");

      var pp = Path.Combine(projectPath, p, "GeneratedFonts");

      var name = Path.GetFileNameWithoutExtension(fontPath);

      var outputPath = Path.Combine(pp, $"{name}-atlas.png");
      var charsetPath = Path.Combine(pp, $"{name}-charset.txt");
      var jsonPath = Path.Combine(pp, $"{name}-layout.json");

      if (!forceReload && Directory.Exists(pp))
      {
        if (File.Exists(outputPath) && File.Exists(charsetPath) && File.Exists(jsonPath))
        {
          Log.Debug("Loading font: " + outputPath + " - " + jsonPath);
          var imageBytes = File.ReadAllBytes(outputPath);
          var fieldFont = FieldFont.FromJsonAndBitmapBytes(jsonPath, imageBytes);
          return fieldFont;
        }
      }

      var msdfgen = Path.Combine(root, "Tools", "msdf-atlas-gen.exe");

      if (File.Exists(msdfgen))
      {
        //TODO: for runtime fallback cache?
        //var appdataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        //appdataPath = Path.Combine(appdataPath, "HelloMonoGame", "CompiledFieldFonts");

        if (!Path.Exists(pp))
        {
          Directory.CreateDirectory(pp);
        }

        //TODO: allow a limited charset
        File.WriteAllText(charsetPath, "[0x0,0xFFFF]"); // Full unicode range

        string arguments = $"-font \"{fontPath}\" -imageout \"{outputPath}\" -type mtsdf -charset \"{charsetPath}\" -size {32} -pxrange {4} -json \"{jsonPath}\" -yorigin top";

        ProcessHelper.RunExe(msdfgen, arguments);

        // if (!File.Exists(outputPath) |¦ !File.Exists(jsonPath))
        if (!File.Exists(outputPath))
        {
          Log.Error("Failed to generate font: " + outputPath);
          return null;
        }

        Log.Debug("Loading font: " + outputPath + " - " + jsonPath);
        var imageBytes = File.ReadAllBytes(outputPath);
        var fieldFont = FieldFont.FromJsonAndBitmapBytes(jsonPath, imageBytes);
        return fieldFont;
        //https://github.com/Peewi/BracketHouse.FontExtension?tab=readme-ov-file
      }

      return null;
    }

    public FieldFont LoadFieldFont(string fontPath, bool forceReload)
    {
      if (!forceReload && ValidatePathAndGetCached(fontPath, out FieldFont cached))
      {
        return cached;
      }

      var root = PathHelper.FindSolutionDirectory();

      var stackTrace = new StackTrace(false);
      var isAot = stackTrace.GetFrame(0)?.GetMethod() is null;

      if (root == null || m_loadAsIfPublish || isAot)
      {
        root = Directory.GetCurrentDirectory();
        var fpath = Path.Combine(root, Path.GetDirectoryName(fontPath));
        var spath = Path.Combine(fpath, "GeneratedFonts");
        var name = Path.GetFileNameWithoutExtension(fontPath);

        var imgPath = Path.Combine(spath, $"{name}-atlas.png");
        var jsonPath = Path.Combine(spath, $"{name}-layout.json");

        Log.Debug("Loading font: " + imgPath + " - " + jsonPath);

        var imageBytes = File.ReadAllBytes(imgPath);
        var fieldFont = FieldFont.FromJsonAndBitmapBytes(jsonPath, imageBytes);
        return fieldFont;
      }

      return GenerateFieldFont(root, fontPath, forceReload);
    }

    /// <summary>
    /// LoadAsync a song from file.
    /// </summary>
    /// <param name="songFile">Song file path.</param>
    /// <returns>MonoGame Song.</returns>
    public Song LoadSong(string songFile, bool forceReload)
    {
      // validate path and get from cache
      if (!forceReload && ValidatePathAndGetCached(songFile, out Song cached))
      {
        return cached;
      }

      // load song
      var name = Path.GetFileNameWithoutExtension(songFile);
      var song = Song.FromUri(name, new Uri(songFile));

      // add to cache and return
      _loadedAssets[songFile] = song;
      return song;
    }

    private readonly object syncLock = new object();

    /// <summary>
    /// LoadAsync a texture from path.
    /// </summary>
    /// <param name="textureFile">Texture file path.</param>
    /// <returns>MonoGame Texture2D.</returns>
    public Texture2D LoadTexture(string textureFile, bool forceReload = false)
    {
      // Console.WriteLine("Loading texture: " + textureFile);
      // validate path and get from cache
      if (!forceReload && ValidatePathAndGetCached(textureFile, out Texture2D cached))
      {
        return cached;
      }

      try
      {
        Texture2D tex;

        lock (syncLock)
        {
          // Console.WriteLine("Loading texture from file: " + textureFile);
          FileStream fileStream = new FileStream(textureFile, FileMode.Open);
          tex = Texture2D.FromStream(_graphics, fileStream);
          fileStream.Dispose();
          // Console.WriteLine("Texture loaded from file: " + textureFile);
        }

        // add to cache and return 
        _loadedAssets[textureFile] = tex;
        return tex;
      }
      catch (Exception e)
      {
        Log.Error(e.ToString());
      }
      // load texture


      return null;
    }

    public AsepriteFile LoadAsepriteFile(string asepriteFile, bool forceReload = false)
    {
      // validate path and get from cache
      if (!forceReload && ValidatePathAndGetCached(asepriteFile, out AsepriteFile cached))
      {
        return cached;
      }

      AsepriteFile aseFile;
      using (Stream stream = TitleContainer.OpenStream(asepriteFile))
      {
        aseFile = AsepriteFileLoader.FromStream(Path.GetFileName(asepriteFile), stream, preMultiplyAlpha: true);
      }

      _loadedAssets[asepriteFile] = aseFile;
      return aseFile;
    }
  }

  /// <summary>
  /// A method to generate / return effect per mesh part.
  /// </summary>
  /// <param name="modelPath">Model path this part belongs to.</param>
  /// <param name="modelContent">Loaded model raw content.</param>
  /// <param name="part">Part instance we want to create effect for.</param>
  /// <returns>Effect instance or null to use default.</returns>
  public delegate Effect EffectsGenerator(string modelPath, ModelContent modelContent, ModelMeshPart part);
}
