using System;
using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Diwali.Editor
{
    /// <summary>Mirrors the Console and compiler messages to Logs/diwali-console.log for review outside the Editor.</summary>
    [InitializeOnLoad]
    static class DiwaliConsoleTap
    {
        const string LogPath = "Logs/diwali-console.log";
        static readonly object Gate = new object();

        static DiwaliConsoleTap()
        {
            Application.logMessageReceivedThreaded += OnLog;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompiled;
            EditorApplication.playModeStateChanged += change => Write("STATE", change.ToString());
        }

        static void OnLog(string message, string stack, LogType type)
        {
            bool failure = type == LogType.Exception || type == LogType.Error || type == LogType.Assert;
            Write(type.ToString().ToUpperInvariant(), failure ? message + "\n" + stack : message);
        }

        static void OnAssemblyCompiled(string assembly, CompilerMessage[] messages)
        {
            foreach (var message in messages)
                if (message.type == CompilerMessageType.Error)
                    Write("COMPILE", $"{message.file}:{message.line} {message.message}");
        }

        static void Write(string kind, string text)
        {
            lock (Gate)
            {
                try { File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff} [{kind}] {text}\n"); }
                catch (IOException) { }
            }
        }
    }
}
