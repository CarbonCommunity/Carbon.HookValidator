using Carbon.Extensions;
using Carbon.Hooks;
using ICSharpCode.Decompiler.CSharp;
using Mono.Cecil;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using UnityEngine;
using Formatting = Newtonsoft.Json.Formatting;

namespace Carbon.Developers
{
    public class HookExporter
    {
        public HookCache CurrentCache { get; private set; }
        public string CacheFile { get; private set; }

        internal List<Hook> _hooks { get; } = new List<Hook> ();

        internal Dictionary<string, CSharpDecompiler> _decompilers { get; } = new Dictionary<string, CSharpDecompiler> ();
        internal void _carbonInit ()
        {
            var community = typeof ( Community ).Assembly;
            var types = community.GetTypes ();
            foreach ( var type in types )
            {
                var customAttribute = type.GetCustomAttribute<Hook> ();
                if ( customAttribute != null )
                {
                    customAttribute.Type = type;
                    _hooks.Add ( customAttribute );
                }
            }
        }
        internal string _getIdentifier ( Hook hook )
        {
            return $"{hook.Name} ({hook.Type.Name})";
        }
        internal string _getSource ( Hook.Patch patch )
        {
            var method = GetMethod ( TypeToDefinition ( patch.Type, patch.Type.Assembly.Location ), patch.Method );
            if ( method == null )
            {
                Console.WriteLine ( $" {patch.Type.FullName}.{patch.Method} failed" );
                return string.Empty;
            }
            var handle = ( System.Reflection.Metadata.MethodDefinitionHandle )MetadataTokens.EntityHandle ( method.MetadataToken.ToInt32 () );

            if ( !_decompilers.TryGetValue ( patch.Type.Assembly.Location, out var decompiler ) )
            {
                _decompilers.Add ( patch.Type.Assembly.Location, decompiler = new CSharpDecompiler ( patch.Type.Assembly.Location, new ICSharpCode.Decompiler.DecompilerSettings () ) );
            }

            return decompiler.DecompileAsString ( handle );
        }

        public void Init ( string cacheFile )
        {
            _carbonInit ();

            CacheFile = cacheFile;

            if ( OsEx.File.Exists ( cacheFile ) ) CurrentCache = JsonConvert.DeserializeObject<HookCache> ( OsEx.File.ReadText ( cacheFile ) );
            else CurrentCache = new HookCache ();
        }

        public bool Fetch ( Action<Hook, Hook.Patch, string, string> onInvalidated, Action<Hook, Hook.Patch, string> onNewEntry )
        {
            var hasChanged = false;

            foreach ( var hook in _hooks )
            {
                var patch = hook.Type.GetCustomAttribute<Hook.Patch> ();
                if ( patch == null ) continue;



                var identifier = _getIdentifier ( hook );
                var source = _getSource ( patch );

                Console.WriteLine ( $"{hook.Name}: {patch.Type.Assembly.Location}" );

                if ( !CurrentCache.Hooks.TryGetValue ( identifier, out var oldSource ) )
                {
                    CurrentCache.Hooks.Add ( identifier, source );
                    onNewEntry?.Invoke ( hook, patch, source );
                    hasChanged = true;
                }
                else
                {
                    if ( oldSource != source )
                    {
                        onInvalidated?.Invoke ( hook, patch, oldSource, source );
                        CurrentCache.Hooks [ identifier ] = source;
                        hasChanged = true;
                    }
                }
            }

            return hasChanged;
        }
        public void Save ()
        {
            OsEx.File.Create ( CacheFile, JsonConvert.SerializeObject ( CurrentCache, Formatting.Indented ) );
        }

        #region Helpers 

        internal Dictionary<string, ModuleDefinition> _modules { get; } = new Dictionary<string, ModuleDefinition> ();

        internal TypeDefinition TypeToDefinition ( Type type, string assemblyFile )
        {
            if ( !_modules.TryGetValue ( assemblyFile, out var module ) )
            {
                _modules.Add ( assemblyFile, module = ModuleDefinition.ReadModule ( new MemoryStream ( File.ReadAllBytes ( assemblyFile ) ) ) );
            }

            return ( TypeDefinition )module.LookupToken ( type.MetadataToken );
        }
        internal MethodDefinition GetMethod ( TypeDefinition self, string method )
        {
            try { return self.Methods.Where ( m => m.Name == method ).First (); } catch { return null; }
        }

        #endregion

        public class HookCache
        {
            public Dictionary<string, string> Hooks = new Dictionary<string, string> ();
        }

        public struct DiffCheck
        {
            public string [] Differences;
            public string Result;

            public DiffCheck Check ( string before, string after )
            {
                before = before.Trim ();
                after = after.Trim ();

                Differences = before.Split ( '\n' ).Except ( after.Split ( '\n' ) ).ToArray ();

                Result = string.Empty;
                var b = before.Split ( '\n' );
                var a = after.Split ( '\n' );
                var tabs = "\t";
                var lni = a.Length.ToString ().Length;
                var lineNumber = lni >= 3 ? "{0:000}" : lni >= 2 ? "{0:00}" : lni >= 1 ? "{0:0} " : "{0:0} ";
                var lineNumberSpace = lni >= 3 ? "   " : lni >= 2 ? "  " : lni >= 1 ? " " : " ";

                for ( int i = 0; i < b.Length; i++ )
                {
                    var ln = string.Format ( lineNumber, i + 1 );
                    if ( ln.StartsWith ( "0" ) ) ln = $" {ln.TrimStart ( '0' )}";

                    if ( i <= a.Length - 1 )
                    {
                        if ( b [ i ] != a [ i ] )
                        {
                            if ( a [ Mathf.Clamp ( i - 1, 0, b.Length - 1 ) ] != b [ i ] )
                            {
                                Result += $"{ln}  [-] {tabs}{b [ i ]}";
                            }
                            Result += $"{lineNumberSpace}  [+] {tabs}{a [ i ]}";
                        }
                        else
                        {
                            Result += $"{ln}      {tabs}{b [ i ]}";
                        }
                    }
                }

                return this;
            }
        }
    }
}
