using Carbon.Components;
using Carbon.Developers;
using Carbon.Extensions;
using System;
using System.IO;

namespace Carbon.HookValidator
{
    internal class Program
    {
        static void Main ( string [] args )
        {
            var path = CommandLineEx.GetArgumentResult ( "-f", AppDomain.CurrentDomain.BaseDirectory );
            var exporter = new HookExporter ();
            exporter.Init ( Path.Combine ( path, ".hooks" ), typeof ( BasePlayer ).Assembly.Location );

            using ( var body = new StringBody () )
            {
                body.Add ( Log ( $"Started..." ) );

                if ( exporter.Fetch ( onInvalidated: ( hook, patch, oldSource, newSource ) =>
                {
                    body.Add ( Log ( $" Rust has modified the '{hook.Name}' hook patch method!" ) );
                    body.Add ( Log ( $" {patch.Type.FullName} -> {patch.Method}" ) );
                    body.Add ( Log ( $" Difference checking..." ) );
                    body.Add ( Log ( $"\n{new HookExporter.DiffCheck ().Check ( oldSource, newSource ).Result}" ) );
                }, onNewEntry: ( hook, patch, source ) =>
                {
                    body.Add ( Log ( $" New hook '{hook.Name}' added. Patches {patch.Type.FullName} -> {patch.Method}" ) );
                } ) )
                {
                    exporter.Save ();
                    body.Add ( Log ( $"Saved hook manifest." ) );
                }

                OsEx.File.Create ( Path.Combine ( path, ".hooks.report.txt" ), body.ToNewLine () );
            }
        }

        static string Log ( object message )
        {
            return $"[{DateTime.Now}] {message}";
        }
    }
}
