using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace WinPEExecutor
{
    class Program
    {
        private const string DB_PATH = @"G:\OperacionesPendientes\operaciones.db";
        private const string LOG_PATH = @"G:\Logs\log_controlapp.txt";

        static void Main()
        {
            Log("[INFO] Iniciando ejecución de backups con DISM...");

            if (!File.Exists(DB_PATH))
            {
                Log($"[ERROR] No se encontró la base de datos en {DB_PATH}");
                return;
            }

            if (!VerificarEscrituraEnG()) return;

            List<Operacion> backups = ObtenerBackupsPendientes();
            Log($"[INFO] Se encontraron {backups.Count} backups pendientes.");

            foreach (var op in backups)
            {
                Log($"[INFO] Procesando backup: {op.Nombre}");

                char letraAsignada = 'O';
                if (!AsignarLetraAVolumen(op.nvolumenorigen, letraAsignada))
                {
                    Log($"[ERROR] No se pudo asignar letra al volumen {op.nvolumenorigen}");
                    continue;
                }

                op.Origen = letraAsignada + ":/";

                bool exito = EjecutarBackupConDISM(op);

                if (exito)
                {
                    ActualizarEstadoProcesado(op.Nombre);
                    Log($"[OK] Backup {op.Nombre} completado y marcado como procesado.\n");
                }
                else
                {
                    Log($"[ERROR] Falló el backup de {op.Nombre}, continuará con el siguiente.");
                }
            }

            Console.Write("¿Desea realizar una restauración de backup ahora? (s/n): ");
            string respuesta = Console.ReadLine()?.Trim().ToLower();

            if (!string.IsNullOrEmpty(respuesta) && respuesta.StartsWith("s"))
            {
                RestaurarImagenInteractiva();
            }
            else
            {
                Console.WriteLine("Operación cancelada.");
            }


            Log("[INFO] Todas las tareas han sido procesadas. Reiniciando en Windows en 10 segundos...");
            System.Threading.Thread.Sleep(10000);
            ReiniciarEnWindows();
        }

        static bool VerificarEscrituraEnG()
        {
            try
            {
                string testPath = @"G:\test_write.txt";
                File.WriteAllText(testPath, "test");
                File.Delete(testPath);
                Log("[INFO] Verificación de escritura en G: exitosa.");
                return true;
            }
            catch (Exception ex)
            {
                Log($"[ERROR] No se puede escribir en G: {ex.Message}");
                return false;
            }
        }

        static List<Operacion> ObtenerBackupsPendientes()
        {
            var lista = new List<Operacion>();

            using var conn = new SqliteConnection($"Data Source={DB_PATH};");
            conn.Open();

            string query = "SELECT nombre, origen, destino, nvolumenorigen FROM operaciones WHERE tipo = 'backup' AND entorno = 'windows' AND estado != 'procesado'";
            using var cmd = new SqliteCommand(query, conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                lista.Add(new Operacion
                {
                    Nombre = reader.GetString(0),
                    Origen = reader.GetString(1),
                    Destino = reader.GetString(2),
                    nvolumenorigen = reader.GetInt32(3)
                });
            }

            return lista;
        }

        static bool AsignarLetraAVolumen(int numeroVolumen, char letra)
        {
            try
            {
                string script = $"select volume {numeroVolumen}\nassign letter={letra}\nexit\n";
                File.WriteAllText("X:\\script.txt", script);

                var proceso = Process.Start(new ProcessStartInfo("diskpart", "/s X:\\script.txt")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                });

                proceso.WaitForExit();
                return true;
            }
            catch (Exception ex)
            {
                Log($"[ERROR] AsignarLetraAVolumen: {ex.Message}");
                return false;
            }
        }

        static bool EjecutarBackupConDISM(Operacion op)
        {
            try
            {
                string destinoFinal = Path.Combine(op.Destino, op.Nombre + ".wim");
                Directory.CreateDirectory(Path.GetDirectoryName(destinoFinal));

                // Eliminar barra invertida al final para evitar error DISM 87
                string origenSanitizado = op.Origen.TrimEnd('\\', '/');

                string argumentos = $"/Capture-Image /ImageFile:\"{destinoFinal}\" /CaptureDir:\"{origenSanitizado}\" /Name:\"{op.Nombre}\" /CheckIntegrity";
                Log($"[INFO] Ejecutando DISM con argumentos: {argumentos}");

                var stopwatch = Stopwatch.StartNew();

                var proceso = new Process();
                proceso.StartInfo.FileName = "dism";
                proceso.StartInfo.Arguments = argumentos;
                proceso.StartInfo.UseShellExecute = false;
                proceso.StartInfo.RedirectStandardOutput = false; // Permite mostrar barra de carga
                proceso.StartInfo.RedirectStandardError = false;
                proceso.StartInfo.CreateNoWindow = false;         // Permite que se vea la ventana con la barra
                proceso.Start();
                proceso.WaitForExit();

                stopwatch.Stop();
                Log($"[INFO] Tiempo total de backup con DISM: {stopwatch.Elapsed.Minutes} min, {stopwatch.Elapsed.Seconds} seg");
                Log($"[INFO] Código de salida DISM: {proceso.ExitCode}");

                return proceso.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Log($"[ERROR] Excepción durante backup: {ex.Message}");
                return false;
            }
        }

        static void ActualizarEstadoProcesado(string nombre)
        {
            try
            {
                using var conn = new SqliteConnection($"Data Source={DB_PATH};");
                conn.Open();

                string update = "UPDATE operaciones SET estado = 'procesado' WHERE nombre = @nombre";
                using var cmd = new SqliteCommand(update, conn);
                cmd.Parameters.AddWithValue("@nombre", nombre);
                int filas = cmd.ExecuteNonQuery();

                Log($"[INFO] Se actualizó el estado de '{nombre}' en la base de datos. Filas afectadas: {filas}");
            }
            catch (Exception ex)
            {
                Log($"[ERROR] No se pudo actualizar el estado del backup '{nombre}': {ex.Message}");
            }
        }

        static void ReiniciarEnWindows()
        {
            try
            {
                Process.Start(new ProcessStartInfo("wpeutil", "reboot")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            catch (Exception ex)
            {
                Log($"[ERROR] No se pudo reiniciar: {ex.Message}");
            }
        }

        static void Log(string mensaje)
        {
            string linea = $"[{DateTime.Now:HH:mm:ss}] {mensaje}";
            Console.WriteLine(linea);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LOG_PATH));
                File.AppendAllText(LOG_PATH, linea + Environment.NewLine);
            }
            catch { }
        }

        class Operacion
        {
            public string Nombre { get; set; }
            public string Origen { get; set; }
            public string Destino { get; set; }
            public int nvolumenorigen { get; set; }
        }

        static void RestaurarImagenInteractiva()
        {
            try
            {
                using var conn = new SqliteConnection($"Data Source={DB_PATH};");
                conn.Open();

                string query = "SELECT nombre, destino, nvolumenorigen FROM operaciones WHERE tipo = 'backup' AND entorno = 'windows' AND estado = 'procesado' ORDER BY fecha DESC";
                using var cmd = new SqliteCommand(query, conn);
                using var reader = cmd.ExecuteReader();

                var backups = new List<Operacion>();
                while (reader.Read())
                {
                    backups.Add(new Operacion
                    {
                        Nombre = reader.GetString(0),
                        Destino = reader.GetString(1),
                        nvolumenorigen = reader.GetInt32(2)
                    });
                }

                if (backups.Count == 0)
                {
                    Log("[INFO] No hay backups procesados para restaurar.");
                    return;
                }

                Console.WriteLine("Backups disponibles para restaurar:");
                for (int i = 0; i < backups.Count; i++)
                {
                    Console.WriteLine($"{i + 1}. {backups[i].Nombre}");
                }

                Console.Write("Seleccione el número del backup a restaurar: ");
                if (!int.TryParse(Console.ReadLine(), out int seleccion) || seleccion < 1 || seleccion > backups.Count)
                {
                    Log("[ERROR] Selección inválida.");
                    return;
                }

                var backup = backups[seleccion - 1];
                char letraDestino = 'O';

                Log($"[INFO] Intentando asignar la letra {letraDestino}: al volumen {backup.nvolumenorigen} para restauración.");
                if (!AsignarLetraAVolumen(backup.nvolumenorigen, letraDestino))
                {
                    Log($"[ERROR] No se pudo asignar letra al volumen {backup.nvolumenorigen} para restauración.");
                    return;
                }

                Log($"[INFO] Letra {letraDestino}: asignada exitosamente al volumen {backup.nvolumenorigen}.");

                Log($"[INFO] Formateando volumen {letraDestino}: antes de restaurar...");
                var format = Process.Start(new ProcessStartInfo("cmd.exe", $"/c format {letraDestino}: /FS:NTFS /Q /Y")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                format.WaitForExit();

                string rutaWim = Path.GetFullPath(Path.Combine(backup.Destino, backup.Nombre + ".wim"));
                Log($"[INFO] Restaurando imagen: {rutaWim}");

                string argumentos = $"/Apply-Image /ImageFile:\"{rutaWim}\" /Index:1 /ApplyDir:{letraDestino}:\\";
                Log($"[INFO] Ejecutando DISM con argumentos: {argumentos}");

                var proceso = new Process();
                proceso.StartInfo.FileName = "dism";
                proceso.StartInfo.Arguments = argumentos;
                proceso.StartInfo.UseShellExecute = false;
                proceso.StartInfo.RedirectStandardOutput = false; // Permite barra de progreso
                proceso.StartInfo.RedirectStandardError = false;
                proceso.StartInfo.CreateNoWindow = false;
                proceso.Start();
                proceso.WaitForExit();

                Log($"[INFO] Código de salida DISM: {proceso.ExitCode}");

                if (proceso.ExitCode == 0)
                {
                    Log("[OK] Restauración completada exitosamente.");
                }
                else
                {
                    Log("[ERROR] Restauración falló.");
                }
            }
            catch (Exception ex)
            {
                Log($"[ERROR] RestaurarImagenInteractiva: {ex.Message}");
            }
        }
    }
}