﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using ClosedXML.Excel;
using Microsoft.Data.Sqlite;

namespace Verificador_de_Arquivo
{
    public class ProcessadorBanco
    {
        private readonly string _arquivoExcel;

        public ProcessadorBanco(string arquivoExcel)
        {
            _arquivoExcel = arquivoExcel;
        }

        public string CriarBancoEDefinirStatus(string caminhoNovoExcel)
        {
            var resultados = new List<string[]>();
            using (var wb = new XLWorkbook(_arquivoExcel))
            {
                var ws = wb.Worksheets.First();
                int ultimaLinha = ws.LastRowUsed().RowNumber();
                for (int i = 2; i <= ultimaLinha; i++)
                {
                    var numeroProcesso = ws.Cell(i, 1).GetString().Trim();
                    var resultadoFase1 = ws.Cell(i, 2).GetString().Trim();
                    var documento = ws.Cell(i, 3).GetString().Trim();
                    resultados.Add(new string[] { numeroProcesso, resultadoFase1, documento });
                }
            }

            string pasta = Path.GetDirectoryName(_arquivoExcel);
            string nomeBanco = Path.Combine(pasta, Path.GetFileNameWithoutExtension(_arquivoExcel) + ".db");

            // Salva o caminho do .db para tentar excluir depois
            string caminhoBanco = nomeBanco;

            // Criação e uso do banco de dados
            using (var conn = new SqliteConnection($"Data Source={nomeBanco}"))
            {
                conn.Open();

                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS Arquivos (
                        Numero_Processo TEXT,
                        Resultado_Fase1 TEXT,
                        Documento TEXT
                    );
                ";
                cmd.ExecuteNonQuery();

                using (var tx = conn.BeginTransaction())
                {
                    foreach (var linha in resultados)
                    {
                        if (string.IsNullOrWhiteSpace(linha[0])) continue;
                        var insert = conn.CreateCommand();
                        insert.CommandText = "INSERT INTO Arquivos VALUES ($num, $fase1, $doc)";
                        insert.Parameters.AddWithValue("$num", linha[0]);
                        insert.Parameters.AddWithValue("$fase1", linha[1]);
                        insert.Parameters.AddWithValue("$doc", linha[2]);
                        insert.ExecuteNonQuery();
                    }
                    tx.Commit();
                }

                var triagemCmd = conn.CreateCommand();
                triagemCmd.CommandText = @"
CREATE VIEW ARQUIVOS_VALIDADOS AS
SELECT
  Numero_Processo,
  MAX(CASE WHEN UPPER(TRIM(Documento)) LIKE 'Auto de Infração%' THEN 'SIM' ELSE NULL END) AS ""1 - Auto Infração"",
  MAX(CASE WHEN UPPER(TRIM(Documento)) LIKE 'Decisão de Cancelamento%' THEN 'SIM' ELSE NULL END) AS ""2 - Decisão de Cancelamento"",
  MAX(CASE WHEN UPPER(TRIM(Documento)) LIKE 'Termo de Arquivamento%' THEN 'SIM' ELSE NULL END) AS ""3 - Termo de Arquivamento"",
  MAX(CASE WHEN UPPER(TRIM(Documento)) LIKE 'Solicitação de Invalidação de Auto de Infração%' THEN 'SIM' ELSE NULL END) AS ""4 - Invalidação de Auto"",
  MAX(CASE 
        WHEN UPPER(TRIM(Documento)) LIKE 'Conclusão de Parcelamento/Reparcelamento%'
        OR UPPER(TRIM(Documento)) LIKE 'Requerimento de Parcelamento%'
        OR UPPER(TRIM(Documento)) LIKE 'Requerimento de Parcelamento/Reparcelamento%'
      THEN 1 ELSE NULL END) AS ""5 - Parcelamento"",
  MAX(CASE WHEN UPPER(TRIM(Documento)) LIKE 'Notificação de Autuação%' THEN 'SIM' ELSE NULL END) AS ""6 - Notificação de Autuação"",
  MAX(CASE 
        WHEN UPPER(REPLACE(TRIM(Documento),'–','-')) LIKE 'AR - Notificação de Autuação%' 
        THEN 'SIM' ELSE NULL END
  ) AS ""7.1 - AR de Autuação"",
  MAX(CASE 
        WHEN UPPER(TRIM(Documento)) LIKE '%Publicação DOU%'
        THEN 'SIM' ELSE NULL END
  ) AS ""7.2 - Publicação DOU Autuação"",
  MAX(CASE WHEN UPPER(TRIM(Documento)) LIKE 'Termo de Preclusão de Prazo de Defesa%' THEN 'SIM' ELSE NULL END) AS ""8.1 - Termo Preclusão de Defesa"",
  MAX(CASE WHEN UPPER(TRIM(Documento)) LIKE 'Decisão de Análise de Defesa%' THEN 'SIM' ELSE NULL END) AS ""8.2 - Decisão de Análise de Defesa"",
  MAX(CASE WHEN UPPER(TRIM(Documento)) LIKE 'Análise de Defesa%' THEN 'SIM' ELSE NULL END) AS ""8.3 - Análise de Defesa"",
  MAX(CASE WHEN UPPER(TRIM(Documento)) LIKE '1ª Notificação de Penalidade%' OR UPPER(TRIM(Documento)) LIKE 'NOTIFICAÇÃO DE MULTA%' THEN 'SIM' ELSE NULL END) AS ""9 - Notificação de Penalidade/Multa"",
  MAX(CASE WHEN 
      UPPER(REPLACE(TRIM(Documento),'–','-')) LIKE 'AR - 1ª Notificação de Penalidade%'
      OR UPPER(REPLACE(TRIM(Documento),'–','-')) LIKE 'AR - Notificação de Multa%'
    THEN 'SIM' ELSE NULL END
  ) AS ""10.1 - AR de Penalidade/Multa"",
  MAX(CASE WHEN 
      UPPER(TRIM(Documento)) LIKE 'Publicação DOU Multa%'
    THEN 'SIM' ELSE NULL END
  ) AS ""10.2 - Publicação DOU Multa"",
  MAX(CASE WHEN UPPER(TRIM(Documento)) LIKE 'Termo de Preclusão de Prazo de Rec%' THEN 'SIM' ELSE NULL END) AS ""11.1 - Termo Preclusão de Recurso"",
  MAX(CASE WHEN UPPER(TRIM(Documento)) LIKE 'Relatoria de 1º Recurso%' OR UPPER(TRIM(Documento)) LIKE 'DECISÃO DE RECURSO%' THEN 'SIM' ELSE NULL END) AS ""11.2 - Decisão de Análise de Recurso"",
  MAX(CASE WHEN UPPER(TRIM(Documento)) LIKE 'Análise Recurso 1º Recurso%' THEN 'SIM' ELSE NULL END) AS ""11.3 - Análise de Recurso"",
  MAX(CASE WHEN 
    UPPER(TRIM(Documento)) LIKE '2ª Notificação de Penalidade%' 
    OR UPPER(TRIM(Documento)) LIKE 'Notificação Final de Multa%'
    OR UPPER(TRIM(Documento)) LIKE 'Notificação Final de Penalidade%'
  THEN 'SIM' ELSE NULL END) AS ""12 - 2ª Notificação Penalidade/Final Multa"",
  MAX(CASE WHEN 
    UPPER(REPLACE(TRIM(Documento),'–','-')) LIKE 'AR - 2ª Notificação de Penalidade%'
    OR UPPER(REPLACE(TRIM(Documento),'–','-')) LIKE 'AR - Notificação de Final Multa%'
  THEN 'SIM' ELSE NULL END) AS ""13 - AR 2ª Penalidade/Final Multa"",
  MAX(Resultado_Fase1) AS Resultado_Fase1
FROM Arquivos
GROUP BY Numero_Processo
";
                triagemCmd.ExecuteNonQuery();

                var statusCmd = conn.CreateCommand();
                statusCmd.CommandText = @"
SELECT *,
    CASE
        WHEN Resultado_Fase1 <> 'SEGUIR' THEN Resultado_Fase1
        WHEN ""2 - Decisão de Cancelamento"" = 'SIM' THEN 'NÃO APTO - EXISTE DECISÃO DE CANCELAMENTO'
        WHEN ""3 - Termo de Arquivamento"" = 'SIM' THEN 'NÃO APTO - EXISTE DECISÃO DE ARQUIVAMENTO'
        WHEN ""4 - Invalidação de Auto"" = 'SIM' THEN 'NÃO APTO - EXISTE INVALIDAÇÃO DE AUTO DE INFRAÇÃO'
        WHEN ""5 - Parcelamento"" = 1 THEN 'NÃO APTO - EXISTE PARCELAMENTO'
        WHEN (""1 - Auto Infração"" IS NULL OR ""1 - Auto Infração"" <> 'SIM') THEN 'NÃO APTO - SEM AUTO DE INFRAÇÃO'
        WHEN (""6 - Notificação de Autuação"" IS NULL OR ""6 - Notificação de Autuação"" <> 'SIM') THEN 'NÃO APTO - SEM NOTIFICAÇÃO DE AUTUAÇÃO'
        WHEN (""9 - Notificação de Penalidade/Multa"" IS NULL OR ""9 - Notificação de Penalidade/Multa"" <> 'SIM') THEN 'NÃO APTO - SEM NOTIFICAÇÃO DE PENALIDADE / MULTA'
        WHEN (""8.1 - Termo Preclusão de Defesa"" IS NULL OR ""8.1 - Termo Preclusão de Defesa"" <> 'SIM')
             AND (""8.2 - Decisão de Análise de Defesa"" IS NULL OR ""8.2 - Decisão de Análise de Defesa"" <> 'SIM')
             AND (""8.3 - Análise de Defesa"" IS NULL OR ""8.3 - Análise de Defesa"" <> 'SIM') THEN 'NÃO APTO - SEM TERMO DE PRECLUSÃO OU DECISÃO DE DEFESA'
        WHEN (""8.1 - Termo Preclusão de Defesa"" IS NULL OR ""8.1 - Termo Preclusão de Defesa"" <> 'SIM')
             AND (""8.2 - Decisão de Análise de Defesa"" IS NULL OR ""8.2 - Decisão de Análise de Defesa"" <> 'SIM')
             AND ""8.3 - Análise de Defesa"" = 'SIM' THEN 'VERIFICAR SE HÁ ANÁLISE DE DEFESA'
        WHEN ""11.1 - Termo Preclusão de Recurso"" = 'SIM' THEN 'APTO'
        WHEN (""11.1 - Termo Preclusão de Recurso"" IS NULL OR ""11.1 - Termo Preclusão de Recurso"" <> 'SIM')
             AND (""11.2 - Decisão de Análise de Recurso"" IS NULL OR ""11.2 - Decisão de Análise de Recurso"" <> 'SIM')
             AND (""11.3 - Análise de Recurso"" IS NULL OR ""11.3 - Análise de Recurso"" <> 'SIM') THEN 'NÃO APTO - SEM TERMO DE PRECLUSÃO OU DECISÃO DE RECURSO'
        WHEN (""11.1 - Termo Preclusão de Recurso"" IS NULL OR ""11.1 - Termo Preclusão de Recurso"" <> 'SIM')
             AND (""11.2 - Decisão de Análise de Recurso"" IS NULL OR ""11.2 - Decisão de Análise de Recurso"" <> 'SIM')
             AND ""11.3 - Análise de Recurso"" = 'SIM' THEN 'VERIFICAR SE HÁ ANÁLISE DE RECURSO'
        WHEN (""11.1 - Termo Preclusão de Recurso"" IS NULL OR ""11.1 - Termo Preclusão de Recurso"" <> 'SIM')
             AND ""11.2 - Decisão de Análise de Recurso"" = 'SIM'
            THEN CASE
                WHEN ""12 - 2ª Notificação Penalidade/Final Multa"" = 'SIM' THEN 'APTO'
                ELSE 'NÃO APTO - SEM 2ª NOTIFICAÇÃO PENALIDADE/FINAL MULTA'
            END
        ELSE 'APTO'
    END AS STATUS_FINAL
FROM ARQUIVOS_VALIDADOS
";

                var resultadosSql = new List<Dictionary<string, string>>();
                using (var reader = statusCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var dict = new Dictionary<string, string>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            dict[reader.GetName(i)] = reader[i]?.ToString();
                        }
                        resultadosSql.Add(dict);
                    }
                }

                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("ResultadoSQL");
                    if (resultadosSql.Count > 0)
                    {
                        int col = 1;
                        foreach (var key in resultadosSql[0].Keys)
                        {
                            worksheet.Cell(1, col++).Value = key;
                        }

                        for (int i = 0; i < resultadosSql.Count; i++)
                        {
                            int c = 1;
                            foreach (var val in resultadosSql[i].Values)
                            {
                                worksheet.Cell(i + 2, c++).Value = val;
                            }
                        }
                    }
                    worksheet.Columns().AdjustToContents();
                    workbook.SaveAs(caminhoNovoExcel);
                }

                conn.Close(); // <-- Força fechamento
            }

            // ======= EXCLUSÃO DA PASTA TEMP =======
            try
            {
                string pastaResultados = Path.GetDirectoryName(caminhoNovoExcel);
                string pastaTemp = Path.Combine(pastaResultados, "TEMP");

                Console.WriteLine("Tentando excluir a pasta: " + pastaTemp);

                if (Directory.Exists(pastaTemp))
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    Thread.Sleep(200);

                    if (File.Exists(caminhoBanco))
                    {
                        try
                        {
                            File.Delete(caminhoBanco);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("⚠️ Erro ao excluir .db: " + ex.Message);
                        }
                    }

                    Directory.Delete(pastaTemp, true);
                    Console.WriteLine("Pasta TEMP excluída com sucesso!");
                    return "Pasta TEMP excluída com sucesso!";
                }
                else
                {
                    return "Pasta TEMP não encontrada para exclusão.";
                }
            }
            catch (Exception ex)
            {
                return "Erro ao excluir a pasta TEMP: " + ex.Message;
            }
        }
    }
}
