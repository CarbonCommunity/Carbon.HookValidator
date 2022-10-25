using Carbon.Extensions;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.OutputVisitor;
using Mono.Cecil;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core.Libraries;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace Carbon.Developers
{
    public class HookGenerator
    {
        public HookManifest Manifest { get; set; }

        public void Init ()
        {
            var path = CommandLineEx.GetArgumentResult ( "-f", AppDomain.CurrentDomain.BaseDirectory );
            var file = Path.Combine ( path, ".hooks.rust" );

            // if ( OsEx.File.Exists ( file ) ) Manifest = JsonConvert.DeserializeObject<HookManifest> ( OsEx.File.ReadText ( file ) );
            Manifest = new HookManifest ();

            var requests = new WebRequests ();

            requests.Enqueue ( "https://raw.githubusercontent.com/OxideMod/Oxide.Rust/develop/resources/Rust.opj", null, delegate ( int error, string data )
            {
                Console.WriteLine ( $"1" );

                typeof ( Core.HookValidator ).GetProperty ( "OxideHooks" ).SetValue ( null, JsonConvert.DeserializeObject<Carbon.Oxide.Metadata.HookPackage> ( data ) );

                foreach ( var manifest in Core.HookValidator.OxideHooks.Manifests )
                {
                    foreach ( var hook in manifest.Hooks )
                    {
                        if ( !Manifest.Hooks.Any ( x => x.Name == hook.Hook.HookName ) )
                        {
                            Manifest.Hooks.Add ( new HookManifest.Hook
                            {
                                Name = hook.Hook.HookName,
                                Category = hook.Hook.HookCategory,
                                Type = hook.Hook.TypeName,
                                Patch = new HookManifest.Hook.HookPatch
                                {
                                    Method = hook.Hook.Signature.Name,
                                    Type = hook.Hook.Signature.ReturnType
                                },
                                Parameters = hook.Hook.Signature.Parameters.Select ( x => new HookManifest.Hook.HookParameter
                                {
                                    Name = char.ToLower ( x [ 0 ] ) + x.Substring ( 1 ).Replace ( ".", "_" ),
                                    Type = x
                                } ).ToList ()
                            } );
                        }
                    }
                }

                requests.Enqueue ( "https://umod.org/documentation/hooks/rust.json", null, ( error1, data1 ) =>
                {
                    var jobject = JsonConvert.DeserializeObject<JObject> ( data1 );
                    var jobjectData = jobject [ "data" ];

                    foreach ( var hook in jobjectData )
                    {
                        var existentHook = Manifest.Hooks.FirstOrDefault ( x => x.Name == hook [ "name" ].ToString () );
                        if ( existentHook == null ) continue;

                        existentHook.Info.Clear ();
                        foreach ( var info in hook [ "description" ].ToString ().Replace ( "<ul>\r\n", "" ).Replace ( "</li>\r\n</ul>", "" ).Split ( new string [] { "</li>\r\n<li>" }, StringSplitOptions.RemoveEmptyEntries ) )
                        {
                            existentHook.Info.Add ( info.Replace ( "<li>", "" ) );
                        }
                    }

                    Console.WriteLine ( $"ajshdajksd" );
                    OsEx.File.Create ( file, JsonConvert.SerializeObject ( Manifest, Formatting.Indented ) );
                    GenerateCsFile ();
                }, null );
            }, null );

            Console.ReadLine ();
        }

        public void GenerateCsFile ()
        {
            var result = string.Empty;
            var sources = new List<string> ();

            foreach ( var hook in Manifest.Hooks )
            {
                var source = _getSource ( hook, out var parameters );
                if ( string.IsNullOrEmpty ( source ) ) continue;

                var lines = source.Trim ().Split ( '\n' );
                var method = string.Empty;
                var endsWithReturn = lines [ lines.Length - 2 ].Contains ( "return " );
                var inMethod = false;

                for ( int i = 1; i < lines.Length - 1; i++ )
                {
                    if ( lines [ i ].StartsWith ( "{" ) ) inMethod = true;
                    if ( !inMethod ) continue;

                    if ( lines [ i ].StartsWith ( "[" )) continue;

                    method += $"{lines [ i ].Replace ( "CancelInvoke (", "CancelInvoke (__instance." ).Replace ( "InvokeRepeating (", "InvokeRepeating (__instance." ).Replace ( "InvokeRandomized (", "InvokeRandomized (__instance." ).Replace ( "this", "__instance" ).Replace ( "base.", "__instance." ).Replace ( "return ", "__result = " ).Replace("return;", "return false;")}\n";
                    if ( lines [ i ].Contains ( "return " ) ) method += $"{lines [ i ].Substring ( 0, lines [ i ].IndexOfAny ( new char [] { 'r' } ) )}return false;\n";
                }

                var initialMethodLine = lines.FirstOrDefault(x => !x.StartsWith("["));
                var isStatic = initialMethodLine.Contains ( " static" );
                var isVoid = initialMethodLine.Contains ( " void" );

                var extraParams = new List<string> ();
                if ( !isStatic ) extraParams.Add ( $"ref {hook.Type.Replace("/", ".")} __instance" );
                if ( !isVoid ) extraParams.Add ( $"ref {hook.Patch.Type.Replace ( "/", "." )} __result" );

                var extraParameters = $"{( parameters.Count > 0 && extraParams.Count > 0 ? ", " : "" )}{extraParams.ToArray ().ToString ( ", " )}";
                sources.Add ( $"public static bool A_{RandomEx.GetRandomString(6)}({parameters.Select ( x => $"{x.ParameterType.FullName.Replace("/", ".")} {x.Name}" ).ToArray ().ToString ( ", " )}{extraParameters})\n{method.TrimEnd ()}\n{( endsWithReturn ? "" : $"\treturn false;\n" )}}}" );
            }

            foreach ( var source in sources )
            {
                result += $"{source}\n\n";
            }

            OsEx.File.Create ( ".cs", result );
        }

        internal Dictionary<string, CSharpDecompiler> _decompilers { get; } = new Dictionary<string, CSharpDecompiler> ();

        internal string _getSource ( HookManifest.Hook patch, out List<ParameterDefinition> parameters )
        {
            parameters = null;

            var type = _findType ( patch.Type );
            if ( type == null ) return null;

            var typeAssembly = type.Assembly;

            var method = HookExporter.GetMethod ( HookExporter.TypeToDefinition ( type, typeAssembly.Location ), patch.Patch.Method );
            if ( method == null )
            {
                Console.WriteLine ( $" {type.FullName}.{patch.Patch.Method} failed" );
                return string.Empty;
            }
            var handle = ( System.Reflection.Metadata.MethodDefinitionHandle )MetadataTokens.EntityHandle ( method.MetadataToken.ToInt32 () );

            if ( !_decompilers.TryGetValue ( typeAssembly.Location, out var decompiler ) )
            {
                _decompilers.Add ( typeAssembly.Location, decompiler = new CSharpDecompiler ( typeAssembly.Location, new ICSharpCode.Decompiler.DecompilerSettings
                {
                    AlwaysQualifyMemberReferences = true,         
                    UseNestedDirectoriesForNamespaces = true,
                    FileScopedNamespaces = true,
                    AggressiveInlining = true,
                    ObjectOrCollectionInitializers = true,
                    ExtensionMethodsInCollectionInitializers = true,
                    UseRefLocalsForAccurateOrderOfEvaluation = true,
                    UseEnhancedUsing = false,
                    UsingDeclarations = false,
                    UsingStatement = false,
                    ExpandUsingDeclarations = false,
                } ) );
            }

            var decompilation = decompiler.Decompile ( handle );
            var style = FormattingOptionsFactory.CreateMono ();
            parameters = method.Parameters.ToList ();
            return decompilation.ToString ( style );
        }
        internal Type _findType ( string type )
        {
            foreach ( var assembly in AppDomain.CurrentDomain.GetAssemblies () )
            {
                if ( assembly.FullName.Contains ( "Assembly" ) ||
                    assembly.FullName.Contains ( "Unity" ) ||
                    assembly.FullName.Contains ( "Facepunch" ) ||
                    assembly.FullName.Contains ( "Rust" ) )
                {
                    foreach ( var t in assembly.GetTypes () )
                    {
                        if ( t.FullName == type.Replace ( "/", "+" ) ) return t;
                    }
                }
            }

            return null;
        }
    }
}