using System.Linq;
using System.Text;
/* Linq 사용시 주의사항
*  
* Linq 는 체이닝 함수의 연산 및 메모리 효율을 위해 지연실행 컨셉 (GetEnumerator 호출시 쿼리 로직 실행함)
* 쿼리를 한번하고 해당데이터 한번 쓰고 말거면 효율좋음
* 근데 데이터를 여러번 써야하면 (GetEnumerator 가 여러번 호출될 상황.. 예를들어서 foreach 문을 몇번 돌려야한다던지 등..) 효율이 안좋으니까 
* Linq 쓰지말아야함.
* 
* List<int> arr = new List<int> { 3, 2, 4 };
* 
* // 아래 sort 후 순회 두번 이상하는 로직 OK
* //-------------------------------------------
* arr.Sort(); // 호출하는 시점에 오름차순정렬됨
* 
* foreach (var item in arr)
* {
*     // Something to do 1
* }
* 
* foreach (var item in arr)
* {
*     // Something to do 2
* }
* 
* // 아래 Orderby 후 순회 두번 이상 하는 로직 Not Ok
* //---------------------------------------------
* IOrderedEnumerable<int> sorted = arr.OrderBy(x => x); // 호출하는시점 (Enumerable 객체 생성시) 에는 정렬안됨
* 
* foreach (int i in sorted)
* {
*     // Something to do 1
* }
* 
* foreach (int i in sorted)
* {
*     // Something to do 2
* }
* 
* // foreach 2 번 실행하는것은 실제로 아래처럼 GetEnumerator 2번 호출된다.
* //--------------------------------------------------------------------------------
* IEnumerator<int> e1 = sorted.GetEnumerator(); // GetEnumerator 호출시 정렬됨 (지연실행 하는이유는 : Linq 체이닝을 효율적으로 하려고)
* 
* while (e1.MoveNext())
* {
*     Console.WriteLine(e1.Current);
* }
* 
* IEnumerator<int> e2 = sorted.GetEnumerator(); // GetEnumerator 호출시 정렬됨 (지연실행 하는이유는 : Linq 체이닝을 효율적으로 하려고)
* 
* while (e2.MoveNext())
* {
*     Console.WriteLine(e2.Current);
* }
* 
* // 아래와 같은 복합 쿼리를 두번 이상 순회해야한다면, ToList() 와 같이 고정자료구조로 변환한다음 하면 쿼리한번으로 재사용 할수있다.
* //----------------------------------------------------------------------------------
* List<int> filtered = arr.OrderBy(x => x)
*                          .Select(x => x * x)
*                          .Where(x => x < 5)
*                          .ToList();
* foreach (var item in filtered)
* {
*     // Something to do 1
* }
* 
* foreach (var item in filtered)
* {
*     // Something to do 2
* }
*/

namespace PacketTool
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // 필요한 args :
            // 1. 어떤 패킷들이 필요한지 정의한 파일 경로
            // 2. 패킷들을 정의한 출력 cs 파일을 추가할 파일 경로

            Console.WriteLine("Start Packet generation");

            if (args.Length != 2)
            {
                Console.WriteLine("패킷 자동생성을 위해서는, def 파일 경로 및 출력 파일 경로가 필요합니다.");
                return;
            }

            #region Packet files
            string defPath = args[0];
            string outDir = args[1];

            if (!File.Exists(defPath))
                throw new FileNotFoundException(defPath);

            if (!Directory.Exists(outDir))
                Directory.CreateDirectory(outDir);

            TypeBuilderForPackets.Build(defPath, outDir);
            #endregion            
        }
    }
}
