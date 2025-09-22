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
            table.Columns.Add("SCRAP_QTY", typeof(int));
            table.Columns.Add("Type", typeof(string)); // 자동 계산용 컬럼
            
            // 초기 데이터 추가 (Type은 SCRAP_CODE에 "CRACK" 포함 여부로 자동 설정)
            AddRowWithAutoType(table, "EVENT001", "A_NORMAL", 2);
            AddRowWithAutoType(table, "EVENT001", "B_CRACK", 2);
            AddRowWithAutoType(table, "EVENT001", "C_CRACK", 1);
            AddRowWithAutoType(table, "EVENT001", "D_NORMAL", 9);
            AddRowWithAutoType(table, "EVENT001", "TOTAL", 6);
            AddRowWithAutoType(table, "EVENT002", "A_NORMAL", 4);
            AddRowWithAutoType(table, "EVENT002", "B_NORMAL", 3);
            AddRowWithAutoType(table, "EVENT002", "C_CRACK", 3);
            AddRowWithAutoType(table, "EVENT002", "D_NORMAL", 3);
            AddRowWithAutoType(table, "EVENT002", "TOTAL", 9);
            AddRowWithAutoType(table, "EVENT003", "A_NORMAL", 4);
            AddRowWithAutoType(table, "EVENT003", "B_CRACK", 6);
            AddRowWithAutoType(table, "EVENT003", "C_CRACK", 5);
            AddRowWithAutoType(table, "EVENT003", "D_NORMAL", 4);
            AddRowWithAutoType(table, "EVENT003", "TOTAL", 7);
            
            return table;
        }
        
        /// <summary>
        /// SCRAP_CODE에 따라 Type을 자동으로 설정하여 행을 추가합니다.
        /// </summary>
        /// <param name="table">DataTable</param>
        /// <param name="eventSeqNo">EVENT_TXT_SEQ_NO</param>
        /// <param name="scrapCode">SCRAP_CODE</param>
        /// <param name="scrapQty">SCRAP_QTY</param>
        static void AddRowWithAutoType(DataTable table, string eventSeqNo, string scrapCode, int scrapQty)
        {
            string type;
            if (scrapCode == "TOTAL")
            {
                type = "TOTAL";
            }
            else if (scrapCode.Contains("CRACK"))
            {
                type = "CRACK";
            }
            else
            {
                type = "NORMAL";
            }
            
            table.Rows.Add(eventSeqNo, scrapCode, scrapQty, type);
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
                .FirstOrDefault(row => row["EVENT_TXT_SEQ_NO"].ToString() == eventSeqNo && row["Type"].ToString() == "TOTAL");
            
            if (totalRow == null)
            {
                Console.WriteLine($"경고: {eventSeqNo}에서 TOTAL 항목을 찾을 수 없습니다.");
                return;
            }
            
            int targetSum = (int)totalRow["SCRAP_QTY"];
            int currentSum = CalculateEventSum(dataTable, eventSeqNo, excludeTotal: true);
            
            Console.WriteLine($"목표 총합: {targetSum}");
            Console.WriteLine($"현재 총합: {currentSum}");
            
            if (currentSum <= targetSum)
            {
                Console.WriteLine("이미 목표 총합 이하입니다. 차감이 필요하지 않습니다.");
                return;
            }
            
            int reductionNeeded = currentSum - targetSum;
            Console.WriteLine($"차감 필요량: {reductionNeeded}");
            
            // 1단계: NORMAL 항목들을 값이 큰 순서대로 1씩 차감
            Console.WriteLine("\n1단계: NORMAL 항목 차감");
            reductionNeeded = ReduceEventNormalItems(dataTable, eventSeqNo, reductionNeeded);
            
            // 2단계: NORMAL 항목들이 모두 0이 되었다면 CRACK 항목들 차감
            if (reductionNeeded > 0 && AllEventNormalItemsAreZero(dataTable, eventSeqNo))
            {
                Console.WriteLine("\n2단계: 모든 NORMAL 항목이 0이 되었으므로 CRACK 항목 차감 시작");
                ReduceEventCrackItems(dataTable, eventSeqNo, reductionNeeded);
            }
            else if (reductionNeeded > 0)
            {
                Console.WriteLine("\n경고: NORMAL 항목이 남아있어 CRACK 항목을 차감할 수 없습니다.");
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
                                 row["Type"].ToString() == "NORMAL" && 
                                 (int)row["SCRAP_QTY"] > 0)
                    .OrderByDescending(row => (int)row["SCRAP_QTY"])
                    .FirstOrDefault();
                
                if (maxNormalRow == null) break; // 차감할 NORMAL 항목이 없음
                
                int currentValue = (int)maxNormalRow["SCRAP_QTY"];
                string scrapCode = maxNormalRow["SCRAP_CODE"].ToString();
                
                maxNormalRow["SCRAP_QTY"] = currentValue - 1;
                reductionNeeded--;
                
                Console.WriteLine($"  {scrapCode}: {currentValue} → {currentValue - 1} (차감량: 1)");
            }
            
            return reductionNeeded;
        }
        
        /// <summary>
        /// 특정 EVENT의 CRACK 항목들을 값이 큰 순서대로 1씩 차감합니다.
        /// </summary>
        /// <param name="dataTable">처리할 DataTable</param>
        /// <param name="eventSeqNo">EVENT_TXT_SEQ_NO</param>
        /// <param name="reductionNeeded">차감 필요량</param>
        static void ReduceEventCrackItems(DataTable dataTable, string eventSeqNo, int reductionNeeded)
        {
            while (reductionNeeded > 0)
            {
                // 매번 값이 가장 큰 CRACK 항목을 찾아서 1씩 차감
                var maxCrackRow = dataTable.AsEnumerable()
                    .Where(row => row["EVENT_TXT_SEQ_NO"].ToString() == eventSeqNo && 
                                 row["Type"].ToString() == "CRACK" && 
                                 (int)row["SCRAP_QTY"] > 0)
                    .OrderByDescending(row => (int)row["SCRAP_QTY"])
                    .FirstOrDefault();
                
                if (maxCrackRow == null) break; // 차감할 CRACK 항목이 없음
                
                int currentValue = (int)maxCrackRow["SCRAP_QTY"];
                string scrapCode = maxCrackRow["SCRAP_CODE"].ToString();
                
                maxCrackRow["SCRAP_QTY"] = currentValue - 1;
                reductionNeeded--;
                
                Console.WriteLine($"  {scrapCode}: {currentValue} → {currentValue - 1} (차감량: 1)");
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
                .Where(row => row["EVENT_TXT_SEQ_NO"].ToString() == eventSeqNo && row["Type"].ToString() == "NORMAL")
                .All(row => (int)row["SCRAP_QTY"] == 0);
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
                query = query.Where(row => row["Type"].ToString() != "TOTAL");
            }
            
            return query.Sum(row => (int)row["SCRAP_QTY"]);
        }
        
        /// <summary>
        /// DataTable의 모든 값의 총합을 계산합니다.
        /// </summary>
        /// <param name="dataTable">계산할 DataTable</param>
        /// <returns>총합</returns>
        static int CalculateSum(DataTable dataTable)
        {
            return dataTable.AsEnumerable()
                .Sum(row => (int)row["SCRAP_QTY"]);
        }
        
        /// <summary>
        /// DataTable의 내용을 EVENT별로 출력합니다.
        /// </summary>
        /// <param name="dataTable">출력할 DataTable</param>
        static void PrintDataTable(DataTable dataTable)
        {
            Console.WriteLine("EVENT_TXT_SEQ_NO\tSCRAP_CODE\t\tSCRAP_QTY\tType");
            Console.WriteLine("--------------------------------------------------------");
            
            // EVENT별로 정렬하여 출력
            var sortedRows = dataTable.AsEnumerable()
                .OrderBy(row => row["EVENT_TXT_SEQ_NO"].ToString())
                .ThenBy(row => row["Type"].ToString() == "TOTAL" ? 1 : 0) // TOTAL 항목을 마지막에 출력
                .ThenBy(row => row["SCRAP_CODE"].ToString());
            
            string currentEvent = "";
            foreach (var row in sortedRows)
            {
                string eventSeqNo = row["EVENT_TXT_SEQ_NO"].ToString();
                string scrapCode = row["SCRAP_CODE"].ToString();
                int scrapQty = (int)row["SCRAP_QTY"];
                string type = row["Type"].ToString();
                
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