using ProgramaControl.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ProgramaControl.Services.Ejecutores
{
    public static class ShutdownExecutor
    {
        public static void Ejecutar()
        {
            Logger.Log("[INFO] Apagando el sistema...");

            string comando = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "shutdown /s /t 0"
                : "poweroff";

            EjecutarComando(comando);
            Logger.Log("[OK] Comando de apagado ejecutado.");
        }

        private static void EjecutarComando(string comando)
        {
            var info = new ProcessStartInfo
            {
                FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd.exe" : "/bin/bash",
                Arguments = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"/c {comando}" : $"-c \"{comando}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proceso = Process.Start(info);
            proceso.WaitForExit();

            if (proceso.ExitCode != 0)
            {
                string error = proceso.StandardError.ReadToEnd();
                throw new Exception($"Error al apagar el sistema: {error}");
            }

            Logger.Log("Apagado del sistema ejecutado correctamente.");
        }
    }
}
