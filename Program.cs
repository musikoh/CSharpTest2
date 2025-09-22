using System;
using System.Data;
using System.Linq;

namespace CSharpTest2
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== DataTable GROUP별 차감 시스템 ===");
            
            // DataTable 생성 및 초기 데이터 설정
            DataTable dataTable = CreateInitialDataTable();
            
            Console.WriteLine("\n초기 상태:");
            PrintDataTable(dataTable);
            
            // 각 GROUP별로 차감 로직 실행
            ProcessAllGroups(dataTable);
            
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
            table.Columns.Add("GROUP", typeof(string));
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("Value", typeof(int));
            table.Columns.Add("Type", typeof(string));
            
            // 초기 데이터 추가
            table.Rows.Add("GROUP1","A_NORMAL", 2, "NORMAL");
            table.Rows.Add("GROUP1","B_CRACK", 2, "CRACK");
            table.Rows.Add("GROUP1","C_CRACK", 1, "CRACK");
            table.Rows.Add("GROUP1","D_NORMAL", 9, "NORMAL");
            table.Rows.Add("GROUP1","TOTAL", 6, "TOTAL");
            table.Rows.Add("GROUP2","A_NORMAL", 4, "NORMAL");
            table.Rows.Add("GROUP2","B_NORMAL", 3, "NORMAL");
            table.Rows.Add("GROUP2","C_CRACK", 3, "CRACK");
            table.Rows.Add("GROUP2","D_NORMAL", 3, "NORMAL");
            table.Rows.Add("GROUP2","TOTAL", 9, "TOTAL");
            table.Rows.Add("GROUP3","A_NORMAL", 4, "NORMAL");
            table.Rows.Add("GROUP3","B_CRACK", 6, "CRACK");
            table.Rows.Add("GROUP3","C_CRACK", 5, "CRACK");
            table.Rows.Add("GROUP3","D_NORMAL", 4, "NORMAL");
            table.Rows.Add("GROUP3","TOTAL", 7, "TOTAL");
            return table;
        }
        
        /// <summary>
        /// 모든 GROUP에 대해 차감 로직을 처리합니다.
        /// </summary>
        /// <param name="dataTable">처리할 DataTable</param>
        static void ProcessAllGroups(DataTable dataTable)
        {
            // 모든 GROUP 목록 가져오기
            var groups = dataTable.AsEnumerable()
                .Select(row => row["GROUP"].ToString())
                .Distinct()
                .Where(group => !string.IsNullOrEmpty(group))
                .ToList();

            foreach (string group in groups)
            {
                Console.WriteLine($"\n=== {group} 처리 ===");
                ProcessGroupReduction(dataTable, group);
            }
        }

        /// <summary>
        /// 특정 GROUP의 차감 로직을 처리합니다.
        /// </summary>
        /// <param name="dataTable">처리할 DataTable</param>
        /// <param name="groupName">처리할 GROUP 이름</param>
        static void ProcessGroupReduction(DataTable dataTable, string groupName)
        {
            // 해당 GROUP의 TOTAL 값 가져오기
            var totalRow = dataTable.AsEnumerable()
                .FirstOrDefault(row => row["GROUP"].ToString() == groupName && row["Type"].ToString() == "TOTAL");
            
            if (totalRow == null)
            {
                Console.WriteLine($"경고: {groupName}에서 TOTAL 항목을 찾을 수 없습니다.");
                return;
            }
            
            int targetSum = (int)totalRow["Value"];
            int currentSum = CalculateGroupSum(dataTable, groupName, excludeTotal: true);
            
            Console.WriteLine($"목표 총합: {targetSum}");
            Console.WriteLine($"현재 총합: {currentSum}");
            
            if (currentSum <= targetSum)
            {
                Console.WriteLine("이미 목표 총합 이하입니다. 차감이 필요하지 않습니다.");
                return;
            }
            
            int reductionNeeded = currentSum - targetSum;
            Console.WriteLine($"차감 필요량: {reductionNeeded}");
            
            // 1단계: NORMAL 항목들을 값이 큰 순서대로 차감
            Console.WriteLine("\n1단계: NORMAL 항목 차감");
            reductionNeeded = ReduceGroupNormalItems(dataTable, groupName, reductionNeeded);
            
            // 2단계: NORMAL 항목들이 모두 0이 되었다면 CRACK 항목들 차감
            if (reductionNeeded > 0 && AllGroupNormalItemsAreZero(dataTable, groupName))
            {
                Console.WriteLine("\n2단계: 모든 NORMAL 항목이 0이 되었으므로 CRACK 항목 차감 시작");
                ReduceGroupCrackItems(dataTable, groupName, reductionNeeded);
            }
            else if (reductionNeeded > 0)
            {
                Console.WriteLine("\n경고: NORMAL 항목이 남아있어 CRACK 항목을 차감할 수 없습니다.");
            }
        }
        
        /// <summary>
        /// 특정 GROUP의 NORMAL 항목들을 값이 큰 순서대로 1씩 차감합니다.
        /// </summary>
        /// <param name="dataTable">처리할 DataTable</param>
        /// <param name="groupName">GROUP 이름</param>
        /// <param name="reductionNeeded">차감 필요량</param>
        /// <returns>남은 차감 필요량</returns>
        static int ReduceGroupNormalItems(DataTable dataTable, string groupName, int reductionNeeded)
        {
            while (reductionNeeded > 0)
            {
                // 매번 값이 가장 큰 NORMAL 항목을 찾아서 1씩 차감
                var maxNormalRow = dataTable.AsEnumerable()
                    .Where(row => row["GROUP"].ToString() == groupName && 
                                 row["Type"].ToString() == "NORMAL" && 
                                 (int)row["Value"] > 0)
                    .OrderByDescending(row => (int)row["Value"])
                    .FirstOrDefault();
                
                if (maxNormalRow == null) break; // 차감할 NORMAL 항목이 없음
                
                int currentValue = (int)maxNormalRow["Value"];
                string itemName = maxNormalRow["Name"].ToString();
                
                maxNormalRow["Value"] = currentValue - 1;
                reductionNeeded--;
                
                Console.WriteLine($"  {itemName}: {currentValue} → {currentValue - 1} (차감량: 1)");
            }
            
            return reductionNeeded;
        }
        
        /// <summary>
        /// 특정 GROUP의 CRACK 항목들을 값이 큰 순서대로 1씩 차감합니다.
        /// </summary>
        /// <param name="dataTable">처리할 DataTable</param>
        /// <param name="groupName">GROUP 이름</param>
        /// <param name="reductionNeeded">차감 필요량</param>
        static void ReduceGroupCrackItems(DataTable dataTable, string groupName, int reductionNeeded)
        {
            while (reductionNeeded > 0)
            {
                // 매번 값이 가장 큰 CRACK 항목을 찾아서 1씩 차감
                var maxCrackRow = dataTable.AsEnumerable()
                    .Where(row => row["GROUP"].ToString() == groupName && 
                                 row["Type"].ToString() == "CRACK" && 
                                 (int)row["Value"] > 0)
                    .OrderByDescending(row => (int)row["Value"])
                    .FirstOrDefault();
                
                if (maxCrackRow == null) break; // 차감할 CRACK 항목이 없음
                
                int currentValue = (int)maxCrackRow["Value"];
                string itemName = maxCrackRow["Name"].ToString();
                
                maxCrackRow["Value"] = currentValue - 1;
                reductionNeeded--;
                
                Console.WriteLine($"  {itemName}: {currentValue} → {currentValue - 1} (차감량: 1)");
            }
        }
        
        /// <summary>
        /// 특정 GROUP의 모든 NORMAL 항목이 0인지 확인합니다.
        /// </summary>
        /// <param name="dataTable">확인할 DataTable</param>
        /// <param name="groupName">GROUP 이름</param>
        /// <returns>해당 GROUP의 모든 NORMAL 항목이 0이면 true</returns>
        static bool AllGroupNormalItemsAreZero(DataTable dataTable, string groupName)
        {
            return dataTable.AsEnumerable()
                .Where(row => row["GROUP"].ToString() == groupName && row["Type"].ToString() == "NORMAL")
                .All(row => (int)row["Value"] == 0);
        }
        
        /// <summary>
        /// 특정 GROUP의 값의 총합을 계산합니다.
        /// </summary>
        /// <param name="dataTable">계산할 DataTable</param>
        /// <param name="groupName">GROUP 이름</param>
        /// <param name="excludeTotal">TOTAL 항목 제외 여부</param>
        /// <returns>총합</returns>
        static int CalculateGroupSum(DataTable dataTable, string groupName, bool excludeTotal = false)
        {
            var query = dataTable.AsEnumerable()
                .Where(row => row["GROUP"].ToString() == groupName);
            
            if (excludeTotal)
            {
                query = query.Where(row => row["Type"].ToString() != "TOTAL");
            }
            
            return query.Sum(row => (int)row["Value"]);
        }
        
        /// <summary>
        /// DataTable의 모든 값의 총합을 계산합니다.
        /// </summary>
        /// <param name="dataTable">계산할 DataTable</param>
        /// <returns>총합</returns>
        static int CalculateSum(DataTable dataTable)
        {
            return dataTable.AsEnumerable()
                .Sum(row => (int)row["Value"]);
        }
        
        /// <summary>
        /// DataTable의 내용을 GROUP별로 출력합니다.
        /// </summary>
        /// <param name="dataTable">출력할 DataTable</param>
        static void PrintDataTable(DataTable dataTable)
        {
            Console.WriteLine("GROUP\t\t항목명\t\t값\t타입");
            Console.WriteLine("----------------------------------------");
            
            // GROUP별로 정렬하여 출력
            var sortedRows = dataTable.AsEnumerable()
                .OrderBy(row => row["GROUP"].ToString())
                .ThenBy(row => row["Type"].ToString() == "TOTAL" ? 1 : 0) // TOTAL 항목을 마지막에 출력
                .ThenBy(row => row["Name"].ToString());
            
            string currentGroup = "";
            foreach (var row in sortedRows)
            {
                string group = row["GROUP"].ToString();
                string name = row["Name"].ToString();
                int value = (int)row["Value"];
                string type = row["Type"].ToString();
                
                if (group != currentGroup)
                {
                    if (!string.IsNullOrEmpty(currentGroup))
                    {
                        Console.WriteLine("----------------------------------------");
                    }
                    currentGroup = group;
                }
                
                Console.WriteLine($"{group}\t\t{name}\t\t{value}\t{type}");
            }
            
            Console.WriteLine("----------------------------------------");
            int totalSum = CalculateSum(dataTable);
            Console.WriteLine($"전체 총합:\t\t\t{totalSum}");
        }
    }
}
