using ProgramaControl.Models;
using ProgramaControl.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProgramaControl.Services.Ejecutores
{
    public static class BackupExecutor
    {
        public static void Ejecutar(Operacion op)
        {
            // Validaciones
            if (string.IsNullOrWhiteSpace(op.Origen))
                throw new ArgumentException("Campo 'origen' vacío en operación backup");

            if (string.IsNullOrWhiteSpace(op.Destino))
                throw new ArgumentException("Campo 'destino' vacío en operación backup");

            string origenNormalizado = Path.GetFullPath(op.Origen.Trim()).TrimEnd(Path.DirectorySeparatorChar);
            if (!Directory.Exists(origenNormalizado))
                throw new DirectoryNotFoundException($"Directorio origen no encontrado: {origenNormalizado}");

            if (origenNormalizado.Length != 2 || origenNormalizado[1] != ':')
                throw new InvalidOperationException($"Backup solo permitido desde volúmenes raíz (ej: 'C:'). Recibido: '{origenNormalizado}'");

            if (op.Destino.StartsWith(op.Origen, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Destino no puede estar dentro del origen (recursión infinita)");

            long tamanoOrigen = CalcularTamanoCarpeta(origenNormalizado);
            long espacioDisponible = ObtenerEspacioDisponible(op.Destino);

            if (tamanoOrigen > espacioDisponible)
                throw new IOException($"Espacio insuficiente: origen ocupa {tamanoOrigen / (1024 * 1024)} MB, destino tiene {espacioDisponible / (1024 * 1024)} MB");

            // Crear imagen .wim con DISM
            string archivoWim = Path.Combine(op.Destino, op.Nombre + ".wim");
            Directory.CreateDirectory(Path.GetDirectoryName(archivoWim));

            string argumentos = $"/Capture-Image /ImageFile:\"{archivoWim}\" /CaptureDir:\"{origenNormalizado}\\\" /Name:\"{op.Nombre}\" /CheckIntegrity";

            Logger.Log($"[INFO] Ejecutando DISM con: {argumentos}");
            var stopwatch = Stopwatch.StartNew();

            var proceso = Process.Start(new ProcessStartInfo("dism", argumentos)
            {
                UseShellExecute = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                CreateNoWindow = false
            });

            proceso.WaitForExit();
            stopwatch.Stop();

            if (proceso.ExitCode != 0)
                throw new Exception($"DISM falló con código {proceso.ExitCode}");

            Logger.Log($"[OK] Backup completado. Tiempo: {stopwatch.Elapsed.Minutes}m {stopwatch.Elapsed.Seconds}s");
            Logger.Log($"[OK] Imagen generada: {archivoWim}");
        }

        private static long CalcularTamanoCarpeta(string ruta)
        {
            long total = 0;
            var stack = new Stack<string>();
            stack.Push(ruta);

            while (stack.Count > 0)
            {
                string actual = stack.Pop();

                try
                {
                    // Archivos
                    foreach (var file in Directory.GetFiles(actual))
                    {
                        try
                        {
                            total += new FileInfo(file).Length;
                        }
                        catch (Exception ex)
                        {
                            Logger.Log($"[WARN] No se pudo acceder al archivo: {file} - {ex.Message}");
                        }
                    }

                    // Subdirectorios
                    foreach (var dir in Directory.GetDirectories(actual))
                    {
                        stack.Push(dir);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"[WARN] Sin acceso al directorio: {actual} - {ex.Message}");
                }
            }

            return total;
        }

        private static long ObtenerEspacioDisponible(string ruta)
        {
            var drive = new DriveInfo(Path.GetPathRoot(ruta)!);
            return drive.AvailableFreeSpace;
        }

        private static void CopiarCarpeta(string origen, string destino)
        {
            foreach (var dir in Directory.GetDirectories(origen, "*", SearchOption.AllDirectories))
            {
                string destinoDir = dir.Replace(origen, destino);
                Directory.CreateDirectory(destinoDir);
            }

            foreach (var file in Directory.GetFiles(origen, "*", SearchOption.AllDirectories))
            {
                string destinoArchivo = file.Replace(origen, destino);
                File.Copy(file, destinoArchivo, true);
            }
        }
    }
}
