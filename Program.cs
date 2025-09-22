using System;
using System.Data;
using System.Linq;

namespace CSharpTest2
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== DataTable EVENT별 차감 시스템 ===");
            
            // DataTable 생성 및 초기 데이터 설정
            DataTable dataTable = CreateInitialDataTable();
            
            Console.WriteLine("\n초기 상태:");
            PrintDataTable(dataTable);
            
            // 각 EVENT별로 차감 로직 실행
            ProcessAllEvents(dataTable);
            
            Console.WriteLine("\n최종 결과:");
            PrintDataTable(dataTable);
        }
        
        /// <summary>
        /// 초기 DataTable을 생성하고 데이터를 설정합니다.
        /// </summary>
        /// <returns>초기화된 DataTable</returns>
        static DataTable CreateInitialDataTable()
        {
            DataTable table = new DataTable();
            table.Columns.Add("EVENT_TXT_SEQ_NO", typeof(string));
            table.Columns.Add("SCRAP_CODE", typeof(string));
            table.Columns.Add("SCRAP_QTY", typeof(string)); // string 타입으로 변경
            
            // 초기 데이터 추가 (SCRAP_QTY를 문자열로 저장)
            table.Rows.Add("EVENT001", "A_NORMAL", "2");
            table.Rows.Add("EVENT001", "B_SCRAP", "2");
            table.Rows.Add("EVENT001", "C_SCRAP", "1");
            table.Rows.Add("EVENT001", "D_NORMAL", "9");
            table.Rows.Add("EVENT001", "TOTAL", "6");
            table.Rows.Add("EVENT002", "A_NORMAL", "4");
            table.Rows.Add("EVENT002", "B_NORMAL", "3");
            table.Rows.Add("EVENT002", "C_SCRAP", "3");
            table.Rows.Add("EVENT002", "D_NORMAL", "3");
            table.Rows.Add("EVENT002", "TOTAL", "9");
            table.Rows.Add("EVENT003", "A_NORMAL", "1");
            table.Rows.Add("EVENT003", "B_SCRAP", "1");
            table.Rows.Add("EVENT003", "C_SCRAP", "1");
            table.Rows.Add("EVENT003", "D_NORMAL", "1");
            table.Rows.Add("EVENT003", "TOTAL", "7");
            
            return table;
        }
        
        /// <summary>
        /// 문자열 SCRAP_QTY를 정수로 안전하게 변환합니다.
        /// </summary>
        /// <param name="scrapQtyString">SCRAP_QTY 문자열</param>
        /// <returns>변환된 정수값, 변환 실패 시 0</returns>
        static int ParseScrapQty(string scrapQtyString)
        {
            if (int.TryParse(scrapQtyString, out int result))
                return result;
            return 0;
        }
        
        /// <summary>
        /// SCRAP_CODE에 따라 타입을 판단합니다.
        /// </summary>
        /// <param name="scrapCode">SCRAP_CODE 값</param>
        /// <returns>타입 문자열 (NORMAL, SCRAP, TOTAL)</returns>
        static string GetTypeFromScrapCode(string scrapCode)
        {
            if (scrapCode == "TOTAL")
                return "TOTAL";
            else if (scrapCode.Contains("SCRAP"))
                return "SCRAP";
            else
                return "NORMAL";
        }
        
        /// <summary>
        /// 모든 EVENT에 대해 차감 로직을 처리합니다.
        /// </summary>
        /// <param name="dataTable">처리할 DataTable</param>
        static void ProcessAllEvents(DataTable dataTable)
        {
            // 모든 EVENT 목록 가져오기
            var events = dataTable.AsEnumerable()
                .Select(row => row["EVENT_TXT_SEQ_NO"].ToString())
                .Distinct()
                .Where(eventSeqNo => !string.IsNullOrEmpty(eventSeqNo))
                .ToList();

            foreach (string eventSeqNo in events)
            {
                Console.WriteLine($"\n=== {eventSeqNo} 처리 ===");
                ProcessEventReduction(dataTable, eventSeqNo);
            }
        }

        /// <summary>
        /// 특정 EVENT의 차감 로직을 처리합니다.
        /// </summary>
        /// <param name="dataTable">처리할 DataTable</param>
        /// <param name="eventSeqNo">처리할 EVENT_TXT_SEQ_NO</param>
        static void ProcessEventReduction(DataTable dataTable, string eventSeqNo)
        {
            // 해당 EVENT의 TOTAL 값 가져오기
            var totalRow = dataTable.AsEnumerable()
                .FirstOrDefault(row => row["EVENT_TXT_SEQ_NO"].ToString() == eventSeqNo && 
                                     row["SCRAP_CODE"].ToString() == "TOTAL");
            
            if (totalRow == null)
            {
                Console.WriteLine($"경고: {eventSeqNo}에서 TOTAL 항목을 찾을 수 없습니다.");
                return;
            }
            
            int targetSum = ParseScrapQty(totalRow["SCRAP_QTY"].ToString());
            int currentSum = CalculateEventSum(dataTable, eventSeqNo, excludeTotal: true);
            
            Console.WriteLine($"목표 총합: {targetSum}");
            Console.WriteLine($"현재 총합: {currentSum}");
            
            if (currentSum < targetSum)
            {
                //여기에 CV 행 추가
                AddCvRow(dataTable, eventSeqNo, targetSum - currentSum);
                return;
            }
            
            // currentSum >= targetSum인 경우 차감 로직 실행
            int reductionNeeded = currentSum - targetSum;
            Console.WriteLine($"차감 필요량: {reductionNeeded}");
            
            // 1단계: NORMAL 항목들을 값이 큰 순서대로 1씩 차감
            Console.WriteLine("\n1단계: NORMAL 항목 차감");
            reductionNeeded = ReduceEventNormalItems(dataTable, eventSeqNo, reductionNeeded);
            
            // 2단계: NORMAL 항목들이 모두 0이 되었다면 SCRAP 항목들 차감
            if (reductionNeeded > 0 && AllEventNormalItemsAreZero(dataTable, eventSeqNo))
            {
                Console.WriteLine("\n2단계: 모든 NORMAL 항목이 0이 되었으므로 SCRAP 항목 차감 시작");
                ReduceEventScrapItems(dataTable, eventSeqNo, reductionNeeded);
            }
            else if (reductionNeeded > 0)
            {
                Console.WriteLine("\n경고: NORMAL 항목이 남아있어 SCRAP 항목을 차감할 수 없습니다.");
            }
        }
        
        /// <summary>
        /// CV 행을 TOTAL 행 위에 추가합니다.
        /// </summary>
        /// <param name="dataTable">처리할 DataTable</param>
        /// <param name="eventSeqNo">EVENT_TXT_SEQ_NO</param>
        /// <param name="difference">차이 수량</param>
        static void AddCvRow(DataTable dataTable, string eventSeqNo, int difference)
        {
            // TOTAL 행을 찾아서 그 위치에 CV 행을 삽입
            var totalRow = dataTable.AsEnumerable()
                .FirstOrDefault(row => row["EVENT_TXT_SEQ_NO"].ToString() == eventSeqNo && 
                                     row["SCRAP_CODE"].ToString() == "TOTAL");
            
            if (totalRow != null)
            {
                // TOTAL 행의 인덱스를 찾기
                int totalRowIndex = dataTable.Rows.IndexOf(totalRow);
                
                // CV 행을 TOTAL 행 위에 삽입
                DataRow newRow = dataTable.NewRow();
                newRow["EVENT_TXT_SEQ_NO"] = eventSeqNo;
                newRow["SCRAP_CODE"] = "CV";
                newRow["SCRAP_QTY"] = difference.ToString();
                
                dataTable.Rows.InsertAt(newRow, totalRowIndex);
                
                Console.WriteLine($"  CV 행 추가: {eventSeqNo}, CV, {difference}");
            }
        }
        
        /// <summary>
        /// 특정 EVENT의 NORMAL 항목들을 값이 큰 순서대로 1씩 차감합니다.
        /// </summary>
        /// <param name="dataTable">처리할 DataTable</param>
        /// <param name="eventSeqNo">EVENT_TXT_SEQ_NO</param>
        /// <param name="reductionNeeded">차감 필요량</param>
        /// <returns>남은 차감 필요량</returns>
        static int ReduceEventNormalItems(DataTable dataTable, string eventSeqNo, int reductionNeeded)
        {
            while (reductionNeeded > 0)
            {
                // 매번 값이 가장 큰 NORMAL 항목을 찾아서 1씩 차감
                var maxNormalRow = dataTable.AsEnumerable()
                    .Where(row => row["EVENT_TXT_SEQ_NO"].ToString() == eventSeqNo && 
                                 GetTypeFromScrapCode(row["SCRAP_CODE"].ToString()) == "NORMAL" && 
                                 ParseScrapQty(row["SCRAP_QTY"].ToString()) > 0)
                    .OrderByDescending(row => ParseScrapQty(row["SCRAP_QTY"].ToString()))
                    .FirstOrDefault();
                
                if (maxNormalRow == null) break; // 차감할 NORMAL 항목이 없음
                
                int currentValue = ParseScrapQty(maxNormalRow["SCRAP_QTY"].ToString());
                string scrapCode = maxNormalRow["SCRAP_CODE"].ToString();
                
                int newValue = currentValue - 1;
                maxNormalRow["SCRAP_QTY"] = newValue.ToString();
                reductionNeeded--;
                
                Console.WriteLine($"  {scrapCode}: {currentValue} → {newValue} (차감량: 1)");
            }
            
            return reductionNeeded;
        }
        
        /// <summary>
        /// 특정 EVENT의 SCRAP 항목들을 값이 큰 순서대로 1씩 차감합니다.
        /// </summary>
        /// <param name="dataTable">처리할 DataTable</param>
        /// <param name="eventSeqNo">EVENT_TXT_SEQ_NO</param>
        /// <param name="reductionNeeded">차감 필요량</param>
        static void ReduceEventScrapItems(DataTable dataTable, string eventSeqNo, int reductionNeeded)
        {
            while (reductionNeeded > 0)
            {
                // 매번 값이 가장 큰 SCRAP 항목을 찾아서 1씩 차감
                var maxScrapRow = dataTable.AsEnumerable()
                    .Where(row => row["EVENT_TXT_SEQ_NO"].ToString() == eventSeqNo && 
                                 GetTypeFromScrapCode(row["SCRAP_CODE"].ToString()) == "SCRAP" && 
                                 ParseScrapQty(row["SCRAP_QTY"].ToString()) > 0)
                    .OrderByDescending(row => ParseScrapQty(row["SCRAP_QTY"].ToString()))
                    .FirstOrDefault();
                
                if (maxScrapRow == null) break; // 차감할 SCRAP 항목이 없음
                
                int currentValue = ParseScrapQty(maxScrapRow["SCRAP_QTY"].ToString());
                string scrapCode = maxScrapRow["SCRAP_CODE"].ToString();
                
                int newValue = currentValue - 1;
                maxScrapRow["SCRAP_QTY"] = newValue.ToString();
                reductionNeeded--;
                
                Console.WriteLine($"  {scrapCode}: {currentValue} → {newValue} (차감량: 1)");
            }
        }
        
        /// <summary>
        /// 특정 EVENT의 모든 NORMAL 항목이 0인지 확인합니다.
        /// </summary>
        /// <param name="dataTable">확인할 DataTable</param>
        /// <param name="eventSeqNo">EVENT_TXT_SEQ_NO</param>
        /// <returns>해당 EVENT의 모든 NORMAL 항목이 0이면 true</returns>
        static bool AllEventNormalItemsAreZero(DataTable dataTable, string eventSeqNo)
        {
            return dataTable.AsEnumerable()
                .Where(row => row["EVENT_TXT_SEQ_NO"].ToString() == eventSeqNo && 
                             GetTypeFromScrapCode(row["SCRAP_CODE"].ToString()) == "NORMAL")
                .All(row => ParseScrapQty(row["SCRAP_QTY"].ToString()) == 0);
        }
        
        /// <summary>
        /// 특정 EVENT의 값의 총합을 계산합니다.
        /// </summary>
        /// <param name="dataTable">계산할 DataTable</param>
        /// <param name="eventSeqNo">EVENT_TXT_SEQ_NO</param>
        /// <param name="excludeTotal">TOTAL 항목 제외 여부</param>
        /// <returns>총합</returns>
        static int CalculateEventSum(DataTable dataTable, string eventSeqNo, bool excludeTotal = false)
        {
            var query = dataTable.AsEnumerable()
                .Where(row => row["EVENT_TXT_SEQ_NO"].ToString() == eventSeqNo);
            
            if (excludeTotal)
            {
                query = query.Where(row => {
                    string scrapCode = row["SCRAP_CODE"].ToString();
                    return scrapCode != "TOTAL" && scrapCode != "CV";
                });
            }
            
            return query.Sum(row => ParseScrapQty(row["SCRAP_QTY"].ToString()));
        }
        
        /// <summary>
        /// DataTable의 모든 값의 총합을 계산합니다.
        /// </summary>
        /// <param name="dataTable">계산할 DataTable</param>
        /// <returns>총합</returns>
        static int CalculateSum(DataTable dataTable)
        {
            return dataTable.AsEnumerable()
                .Sum(row => ParseScrapQty(row["SCRAP_QTY"].ToString()));
        }
        
        /// <summary>
        /// DataTable의 내용을 EVENT별로 출력합니다.
        /// </summary>
        /// <param name="dataTable">출력할 DataTable</param>
        static void PrintDataTable(DataTable dataTable)
        {
            Console.WriteLine("EVENT_TXT_SEQ_NO\tSCRAP_CODE\t\tSCRAP_QTY\t타입");
            Console.WriteLine("--------------------------------------------------------");
            
            // EVENT별로 정렬하여 출력
            var sortedRows = dataTable.AsEnumerable()
                .OrderBy(row => row["EVENT_TXT_SEQ_NO"].ToString())
                .ThenBy(row => {
                    string scrapCode = row["SCRAP_CODE"].ToString();
                    if (scrapCode == "TOTAL") return 2; // TOTAL을 가장 마지막에
                    else if (scrapCode == "CV") return 1; // CV를 TOTAL 바로 위에
                    else return 0; // 나머지는 먼저
                })
                .ThenBy(row => row["SCRAP_CODE"].ToString());
            
            string currentEvent = "";
            foreach (var row in sortedRows)
            {
                string eventSeqNo = row["EVENT_TXT_SEQ_NO"].ToString();
                string scrapCode = row["SCRAP_CODE"].ToString();
                string scrapQtyStr = row["SCRAP_QTY"].ToString();
                int scrapQty = ParseScrapQty(scrapQtyStr);
                string type = GetTypeFromScrapCode(scrapCode);
                
                if (eventSeqNo != currentEvent)
                {
                    if (!string.IsNullOrEmpty(currentEvent))
                    {
                        Console.WriteLine("--------------------------------------------------------");
                    }
                    currentEvent = eventSeqNo;
                }
                
                Console.WriteLine($"{eventSeqNo}\t\t{scrapCode}\t\t{scrapQty}\t\t{type}");
            }
            
            Console.WriteLine("--------------------------------------------------------");
            int totalSum = CalculateSum(dataTable);
            Console.WriteLine($"전체 총합:\t\t\t\t{totalSum}");
        }
    }
}