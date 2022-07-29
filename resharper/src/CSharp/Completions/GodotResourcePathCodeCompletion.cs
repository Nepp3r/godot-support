﻿using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using JetBrains.DocumentModel;
using JetBrains.Metadata.Reader.API;
using JetBrains.Metadata.Reader.Impl;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.CodeCompletion;
using JetBrains.ReSharper.Feature.Services.CodeCompletion.Infrastructure;
using JetBrains.ReSharper.Feature.Services.CodeCompletion.Infrastructure.LookupItems;
using JetBrains.ReSharper.Feature.Services.CodeCompletion.Infrastructure.LookupItems.Impl;
using JetBrains.ReSharper.Feature.Services.CSharp.CodeCompletion.Infrastructure;
using JetBrains.ReSharper.Feature.Services.Lookup;
using JetBrains.ReSharper.Features.Intellisense.CodeCompletion.CSharp.Rules;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.CSharp.Util;
using JetBrains.ReSharper.Psi.CSharp.Util.Literals;
using JetBrains.ReSharper.Psi.ExpectedTypes;
using JetBrains.ReSharper.Psi.Resources;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.ReSharper.Psi.Util;
using JetBrains.ReSharper.PsiGen;
using JetBrains.TextControl;
using JetBrains.UI.Icons;
using JetBrains.UI.RichText;
using JetBrains.Util;
using JetBrains.Util.Special;

namespace JetBrains.ReSharper.Plugins.Godot.CSharp.Completions
{
    [Language(typeof(CSharpLanguage))]
    public class GodotResourcePathCodeCompletion : CSharpItemsProviderBase<CSharpCodeCompletionContext>
    {
        protected override bool IsAvailable(CSharpCodeCompletionContext context)
        {
            return context.BasicContext.CodeCompletionType == CodeCompletionType.BasicCompletion;
        }

        protected override bool AddLookupItems(CSharpCodeCompletionContext context, IItemsCollector collector)
        {
            try
            {
                var projectPath = context.ProjectPath();
                if (projectPath is null) 
                    return false;
                var stringLiteral = context.StringLiteral();
                if (stringLiteral is null)
                    return false;

                var prefix = "res://";
                if (!(stringLiteral.ConstantValue.AsString() is string originalString
                      && originalString.StartsWith(prefix)))
                {
                    return false;
                }

                var relativePathString = originalString.Substring(prefix.Length);
                var searchPath = VirtualFileSystemPath.ParseRelativelyTo(relativePathString, projectPath);

                // If path leads outside project (e.g., due to `..` going up too many levels), don't provide completions.
                if (!searchPath.IsDescendantOf(projectPath))
                {
                    return false;
                }

                // Populate completions
                var completions = Enumerable.Empty<string>();

                // Scenes
                completions = ConcatFullPathCompletionsForType(completions, context, searchPath, GodotTypes.PackedScene, "tscn");

                // Crypto keys
                // https://github.com/godotengine/godot/blob/297241f368632dd91a3e7df47da3d9e5197e4f1e/core/crypto/crypto.cpp#L168
                completions = ConcatFullPathCompletionsForType(completions, context, searchPath, GodotTypes.X509Certificate, "crt");
                completions = ConcatFullPathCompletionsForType(completions, context, searchPath, GodotTypes.CryptoKey, "key", "pub");

                // AudioStreamMP3 (.mp3)
                // https://github.com/godotengine/godot/blob/a5bc65bbadad814a157283749c1ef8552f1663c4/modules/minimp3/resource_importer_mp3.cpp#L50
                completions = ConcatFullPathCompletionsForType(completions, context, searchPath, GodotTypes.AudioStreamMP3, "mp3");

                // AudioStreamWAV (.wav)
                // https://github.com/godotengine/godot/blob/1c820f19b1a0ba72316896ad354cb31391638a3b/editor/import/resource_importer_wav.cpp#L50
                // AudioStreamSample is older type name: https://github.com/godotengine/godot/blob/1c0d9eef7a697af1e9142a2a95c824c9554a6ca1/editor/import/resource_importer_wav.cpp#L58
                completions = ConcatFullPathCompletionsForTypes(completions, context, searchPath, new[] { GodotTypes.AudioStreamWAV, GodotTypes.AudioStreamSample }, "wav");

                // AudioStreamOggVorbis (.ogg)
                // https://github.com/godotengine/godot/blob/2bf8c4a6d0c553d450695d1988fac39df638ad9a/modules/vorbis/resource_importer_ogg_vorbis.cpp#L52
                completions = ConcatFullPathCompletionsForType(completions, context, searchPath, GodotTypes.AudioStreamOggVorbis, "ogg");

                // AudioStream
                completions = ConcatFullPathCompletionsForType(completions, context, searchPath, GodotTypes.AudioStream,
                    "mp3",
                    "wav",
                    "ogg");

                // Images
                // PNG     (.png):        https://github.com/godotengine/godot/blob/dcdc6954f89876249bd0600b154a900e5bf83d36/drivers/png/image_loader_png.cpp#L55
                // JPG     (.jpg, .jpeg): https://github.com/godotengine/godot/blob/40c360b8703449bb6a3299878600fab45abf9f86/modules/jpg/image_loader_jpegd.cpp#L122
                // BMP     (.bmp)         https://github.com/godotengine/godot/blob/dcdc6954f89876249bd0600b154a900e5bf83d36/modules/bmp/image_loader_bmp.cpp#L290
                // SVG     (.svg):        https://github.com/godotengine/godot/blob/79463aa5defb083569d193658a62755223f14dc4/modules/svg/image_loader_svg.cpp#L136
                // WebP    (.webp):       https://github.com/godotengine/godot/blob/c18d0f20357a11bd9cfa2f57b8b9b500763413bc/modules/webp/image_loader_webp.cpp#L66
                // HDR     (.hdr):        https://github.com/godotengine/godot/blob/d27f60f0e8d78059f8d075e16f0d242a7673bba0/modules/hdr/image_loader_hdr.cpp#L149
                // OpenEXR (.exr):        https://github.com/godotengine/godot/blob/d27f60f0e8d78059f8d075e16f0d242a7673bba0/modules/tinyexr/image_loader_tinyexr.cpp#L292
                // TGA     (.tga):        https://github.com/godotengine/godot/blob/dcdc6954f89876249bd0600b154a900e5bf83d36/modules/tga/image_loader_tga.cpp#L337
                completions = ConcatFullPathCompletionsForTypes(completions, context, searchPath, 
                    new[] {
                        GodotTypes.Texture,
                        GodotTypes.StreamTexture,
                    }, 
                    "png", 
                    "jpg", "jpeg",
                    "bmp",
                    "svg",
                    "webp", 
                    "hdr",
                    "exr",
                    "tga");

                // DDS (.dds): https://github.com/godotengine/godot/blob/d26442e709f6361af9ac755ec9291bb43f2cd69b/modules/dds/texture_loader_dds.cpp#L431
                completions = ConcatFullPathCompletionsForTypes(completions, context, searchPath, 
                    new[] {
                        GodotTypes.Texture,
                        GodotTypes.ImageTexture,
                    }, 
                    "dds");

                // VideoStreamTheora
                // https://github.com/godotengine/godot/blob/d26442e709f6361af9ac755ec9291bb43f2cd69b/modules/theora/video_stream_theora.cpp#L694
                completions = ConcatFullPathCompletionsForTypes(completions, context, searchPath, 
                    new[] {
                        GodotTypes.VideoStreamTheora,
                        GodotTypes.VideoStream
                    },
                    "ogv");

                // Translations
                // https://github.com/godotengine/godot/blob/297241f368632dd91a3e7df47da3d9e5197e4f1e/core/io/translation_loader_po.cpp#L359
                completions = ConcatFullPathCompletionsForType(completions, context, searchPath, GodotTypes.Translation, "po", "mo");

                // Scripts
                // GodotScript (.gd): https://github.com/godotengine/godot/blob/ba3734e69a2f2a4f6c4f908958268762fd805cd2/modules/gdscript/gdscript.cpp#L2410
                completions = ConcatFullPathCompletionsForType(completions, context, searchPath, GodotTypes.GDScript, "gd");
                // C#          (.cs): https://github.com/godotengine/godot/blob/14d021287bced6a7f5ab9db24936bd07b4cfdfd0/modules/mono/csharp_script.cpp#L1238
                completions = ConcatFullPathCompletionsForType(completions, context, searchPath, GodotTypes.CSharpScript, "cs");


                completions = completions.Concat(PathCompletions(searchPath));

                var items = 
                    (from completion in completions.Distinct() 
                     select new ResourcePathItem(originalString, completion, context.CompletionRanges))
                    .ToList();
                foreach (var item in items)
                {
                    collector.Add(item);
                }
                return !items.IsEmpty();
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool ExpectsResource(CSharpCodeCompletionContext context, IClrTypeName resourceType)
        {
            // Get invocation which is using the string being completed
            if (!(
                InvocationExpressionNavigator.GetByArgument(
                    CSharpArgumentNavigator.GetByValue(
                        context.NodeInFile.Parent as ICSharpLiteralExpression))
                is IInvocationExpression invocation))
            {
                return false;
            }

            // GD.Load or ResourceLoader.Load expecting given resource type
            if (invocation.IsGodotLoad(resourceType))
            {
                return true;
            }

            // Load with no generic type argument but being assigned to given resource type
            return AssignmentExpressionNavigator.GetBySource(invocation)
                    is IAssignmentExpression assignment 
                   && assignment.Dest.Type() is IDeclaredType lhsType
                   && GodotTypes.PackedScene.Equals(lhsType.GetClrName())
                   && invocation.IsGodotLoad(null);
        }

        private static IEnumerable<string> ConcatFullPathCompletionsForType(
            IEnumerable<string> completions,
            CSharpCodeCompletionContext context, 
            VirtualFileSystemPath path,
            IClrTypeName typeName,
            params string[] extensions)
        =>
            ExpectsResource(context, typeName)
                ? completions.Concat(ResourceFiles(path, extensions)) 
                : completions;
        private static IEnumerable<string> ConcatFullPathCompletionsForTypes(
            IEnumerable<string> completions,
            CSharpCodeCompletionContext context, 
            VirtualFileSystemPath path,
            IEnumerable<IClrTypeName> typeNames,
            params string[] extensions)
        =>
            typeNames.Aggregate(completions, (c, type) =>
                ConcatFullPathCompletionsForType(c, context, path, type, extensions));

        // Suggests child files or directories of the given path
        // with the aim of completing a path to any existing regular file.
        // If path is an existing regular file, the path is complete, so no suggestions are returned.
        // Otherwise, lists the entries of the last directory in the path.
        private static IEnumerable<string> PathCompletions(VirtualFileSystemPath path)
        {
            var searchDir = SearchDir(path);
            if (searchDir is null)
            {
                return Enumerable.Empty<string>();
            }

            return
                from child in searchDir.GetChildren()
                select child.GetAbsolutePath().Name;
        }

        private static VirtualFileSystemPath SearchDir(VirtualFileSystemPath path)
        {
            switch (path.Exists)
            {
                case FileSystemPath.Existence.Directory: return path;
                case FileSystemPath.Existence.Missing:   return path.Parent;
                default:                                 return null;
            }
        }

        private static IEnumerable<string> ResourceFiles(VirtualFileSystemPath path, string[] extensions)
        {
            var searchDir = SearchDir(path);
            if (searchDir is null)
            {
                return Enumerable.Empty<string>();
            }

            return
                from p in ResourceFilesInner(searchDir, extensions)
                select p.MakeRelativeTo(path).ToString().Replace('\\', '/');
        }

        private static IEnumerable<VirtualFileSystemPath> ResourceFilesInner(VirtualFileSystemPath path, string[] extensions)
        {
            if (path.ExistsFile && extensions.Any(ext => ext.Equals(path.ExtensionNoDot)))
            {
                return new[] { path };
            }

            if (path.ExistsDirectory)
            {
                return
                    path.GetChildren()
                        .SelectMany(child => ResourceFilesInner(child.GetAbsolutePath(), extensions));
            }

            return Enumerable.Empty<VirtualFileSystemPath>();
        }

        private sealed class ResourcePathItem : TextLookupItemBase
        {
            private readonly string myDisplayName;

            public ResourcePathItem(string originalFilePathString, string completion, TextLookupRanges ranges)
            {
                myDisplayName = completion;
                Ranges = ranges;
                var text = $"\"{originalFilePathString}{completion}\"";
                Text = text;
            }

            protected override RichText GetDisplayName() => LookupUtil.FormatLookupString(myDisplayName, TextColor);

            public override IconId Image => PsiSymbolsThemedIcons.Const.Id;

            protected override void OnAfterComplete(ITextControl textControl, ref DocumentRange nameRange, ref DocumentRange decorationRange,
                TailType tailType, ref Suffix suffix, ref IRangeMarker caretPositionRangeMarker)
            {
                base.OnAfterComplete(textControl, ref nameRange, ref decorationRange, tailType, ref suffix, ref caretPositionRangeMarker);
                // Consistently move caret to end of path; i.e., end of the string literal, before closing quote
                textControl.Caret.MoveTo(Ranges.ReplaceRange.StartOffset + Text.Length - 1, CaretVisualPlacement.DontScrollIfVisible);
            }

            public override void Accept(
                ITextControl textControl, DocumentRange nameRange, LookupItemInsertType insertType,
                Suffix suffix, ISolution solution, bool keepCaretStill)
            {
                // Force replace + keep caret still in order to place caret at consistent position (see override of OnAfterComplete)
                base.Accept(textControl, nameRange, LookupItemInsertType.Replace, suffix, solution, true);
            }
        }
    }

    static class GodotTypes
    {
        private static IClrTypeName GodotTypeName(string typeName)
            => new ClrTypeName($"Godot.{typeName}");

        public static readonly IClrTypeName GD                    = GodotTypeName("GD");
        public static readonly IClrTypeName ResourceLoader        = GodotTypeName("ResourceLoader");
        public static readonly IClrTypeName PackedScene           = GodotTypeName("PackedScene");
        public static readonly IClrTypeName X509Certificate       = GodotTypeName("X509Certificate");
        public static readonly IClrTypeName CryptoKey             = GodotTypeName("CryptoKey");
        public static readonly IClrTypeName StreamTexture         = GodotTypeName("StreamTexture");
        public static readonly IClrTypeName ImageTexture          = GodotTypeName("ImageTexture");
        public static readonly IClrTypeName Texture               = GodotTypeName("Texture");
        public static readonly IClrTypeName AudioStreamMP3        = GodotTypeName("AudioStreamMP3");
        public static readonly IClrTypeName AudioStreamWAV        = GodotTypeName("AudioStreamWAV");
        public static readonly IClrTypeName AudioStreamSample     = GodotTypeName("AudioStreamSample");
        public static readonly IClrTypeName AudioStreamOggVorbis  = GodotTypeName("AudioStreamOggVorbis");
        public static readonly IClrTypeName AudioStream           = GodotTypeName("AudioStream");
        public static readonly IClrTypeName Translation           = GodotTypeName("Translation");
        public static readonly IClrTypeName VideoStream           = GodotTypeName("VideoStream");
        public static readonly IClrTypeName VideoStreamTheora     = GodotTypeName("VideoStreamTheora");
        public static readonly IClrTypeName GDScript              = GodotTypeName("GDScript");
        public static readonly IClrTypeName CSharpScript          = GodotTypeName("CSharpScript");
    }

    static class CompletionExtensions
    {
        public static VirtualFileSystemPath ProjectPath(this CSharpCodeCompletionContext context)
            => context.BasicContext.Solution.SolutionProject.ProjectLocationLive.Value;

        public static ICSharpLiteralExpression StringLiteral(this CSharpCodeCompletionContext context)
            => context.NodeInFile is ITokenNode nodeInFile
               && nodeInFile.Parent is ICSharpLiteralExpression literalExpression
               && literalExpression.Literal.IsAnyStringLiteral()
                    ? literalExpression
                    : null;

        public static bool IsDescendantOf(this VirtualFileSystemPath path, VirtualFileSystemPath other)
            => path.FullPath.StartsWith(other.FullPath);


        public static bool IsGodotLoad(
            this IInvocationExpression invocation,
            [CanBeNull] IClrTypeName typeArg)
        {
            var typeArgs = 
                typeArg is IClrTypeName ta
                    ? new[] { ta } 
                    : new IClrTypeName[] { };
            return invocation.InvokesMethod(GodotTypes.GD,             "Load", typeArgs)
                || invocation.InvokesMethod(GodotTypes.ResourceLoader, "Load", typeArgs);
        }

        public static bool InvokesMethod(
            this IInvocationExpression invocationExpression, 
            IClrTypeName typeName,
            string name,
            params IClrTypeName[] expectedTypeArgs)
        {
            var actualTypeArgs = invocationExpression.Reference.Invocation.TypeArguments;
            var typeArgsMatch = 
                actualTypeArgs.Count == expectedTypeArgs.Length
                 && Enumerable.Range(0, expectedTypeArgs.Length)
                    .All(i =>
                        actualTypeArgs[i] is IDeclaredType dt
                         && dt.GetClrName().Equals(expectedTypeArgs[i]));
            return invocationExpression.Reference.Resolve().DeclaredElement is IMethod method
                   && name.Equals(method.ShortName)
                   && method.ContainingType is ITypeElement type
                   && type.GetClrName().Equals(typeName)
                   && typeArgsMatch;
        }
    }

}