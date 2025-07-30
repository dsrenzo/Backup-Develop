using ProgramaControl.Models;
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
    public static class BootExecutor
    {
        public static void Ejecutar(Operacion op)
        {
            Logger.Log("[INFO] Ejecutando cambio de arranque...");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                CambiarArranqueWindows();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                CambiarArranqueLinux();
            }
            else
            {
                throw new NotSupportedException("Sistema operativo no soportado.");
            }

            Logger.Log("[OK] Cambio de arranque aplicado.");
        }

        private static void CambiarArranqueWindows()
        {
            // Ejemplo: cambia el boot a una entrada llamada "WinPE"
            string comando = "bcdedit /default {ID_WINPE}"; // Reemplazar por el GUID real
            EjecutarComando(comando);
        }

        private static void CambiarArranqueLinux()
        {
            // Ejemplo: cambia arranque por grub-set-default 0 (menú GRUB, entrada 0)
            string comando = "grub-set-default 0";
            EjecutarComando(comando);
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
                throw new Exception($"Error al ejecutar comando: {error}");
            }

            Logger.Log("Cambio de arranque ejecutado correctamente.");
        }
    }
}
