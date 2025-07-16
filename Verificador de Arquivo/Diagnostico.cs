using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Verificador_de_Arquivo
{
    public class Diagnostico
    {
        public static async Task ExecutarDiagnostico()
        {
            var resultados = new System.Text.StringBuilder();
            resultados.AppendLine("🔍 DIAGNÓSTICO DO SISTEMA");
            resultados.AppendLine(new string('=', 50));

            // 1. Verificar .NET
            resultados.AppendLine("\n📦 VERIFICANDO .NET:");
            try
            {
                var version = Environment.Version;
                resultados.AppendLine($"✅ .NET Version: {version}");
            }
            catch (Exception ex)
            {
                resultados.AppendLine($"❌ Erro .NET: {ex.Message}");
            }

            // 2. Verificar Chrome
            resultados.AppendLine("\n🌐 VERIFICANDO CHROME:");
            var chromePaths = new[]
            {
                @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe"
            };

            bool chromeEncontrado = false;
            foreach (var path in chromePaths)
            {
                if (File.Exists(path))
                {
                    resultados.AppendLine($"✅ Chrome encontrado: {path}");
                    chromeEncontrado = true;
                    break;
                }
            }

            if (!chromeEncontrado)
            {
                resultados.AppendLine("❌ Chrome não encontrado!");
            }

            // 3. Verificar conectividade
            resultados.AppendLine("\n🌍 VERIFICANDO CONECTIVIDADE:");
            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(10);
                    var response = await client.GetAsync("https://portal.antt.gov.br/sei");
                    if (response.IsSuccessStatusCode)
                    {
                        resultados.AppendLine("✅ Portal SEI acessível");
                    }
                    else
                    {
                        resultados.AppendLine($"⚠️ Portal SEI retornou: {response.StatusCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                resultados.AppendLine($"❌ Erro de conectividade: {ex.Message}");
            }

            // 4. Verificar permissões de arquivo
            resultados.AppendLine("\n📁 VERIFICANDO PERMISSÕES:");
            try
            {
                var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                var pastaTeste = Path.Combine(desktop, "Teste_Permissao");
                
                if (!Directory.Exists(pastaTeste))
                {
                    Directory.CreateDirectory(pastaTeste);
                    resultados.AppendLine("✅ Permissão para criar pasta: OK");
                    Directory.Delete(pastaTeste);
                }
                else
                {
                    resultados.AppendLine("✅ Pasta de teste já existe");
                }
            }
            catch (Exception ex)
            {
                resultados.AppendLine($"❌ Erro de permissão: {ex.Message}");
            }

            // 5. Verificar arquivos do projeto
            resultados.AppendLine("\n📋 VERIFICANDO ARQUIVOS DO PROJETO:");
            
            var tessdataPath = Path.Combine(Directory.GetCurrentDirectory(), "tessdata", "por.traineddata");
            if (File.Exists(tessdataPath))
            {
                var fileInfo = new FileInfo(tessdataPath);
                resultados.AppendLine($"✅ Tesseract: {fileInfo.Length / 1024 / 1024} MB");
            }
            else
            {
                resultados.AppendLine("❌ Arquivo Tesseract não encontrado");
            }

            var exePath = Path.Combine(Directory.GetCurrentDirectory(), "bin", "Debug", "net8.0-windows", "Verificador de Arquivo.exe");
            if (File.Exists(exePath))
            {
                resultados.AppendLine("✅ Executável compilado encontrado");
            }
            else
            {
                resultados.AppendLine("❌ Executável não encontrado");
            }

            // 6. Verificar espaço em disco
            resultados.AppendLine("\n💾 VERIFICANDO ESPAÇO EM DISCO:");
            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)));
                var freeSpaceGB = drive.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0;
                resultados.AppendLine($"✅ Espaço livre: {freeSpaceGB:F1} GB");
                
                if (freeSpaceGB < 1.0)
                {
                    resultados.AppendLine("⚠️ Pouco espaço em disco (< 1 GB)");
                }
            }
            catch (Exception ex)
            {
                resultados.AppendLine($"❌ Erro ao verificar disco: {ex.Message}");
            }

            resultados.AppendLine("\n" + new string('=', 50));
            resultados.AppendLine("🎯 DIAGNÓSTICO CONCLUÍDO");

            MessageBox.Show(resultados.ToString(), "Diagnóstico do Sistema", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
} 