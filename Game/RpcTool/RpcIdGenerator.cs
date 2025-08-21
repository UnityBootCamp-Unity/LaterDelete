/*
 * Attribute : 특성에 대한 메타데이터를 추가하기위한 클래스
 * Attribute 의 메타데이터는 어셈블리단위 파일 (dll, exe) 에 저장됨.
 * 일종의 RpcManifest.json 같은거 만들던지, RpcId table 정의한 class 를 generate 하던지 해서
 * 이걸로 RpcImpl Attribute 메타데이터를 초기화할때 참조하도록 해야함.
 */
using Game.NetworkContracts;
using System.Reflection;
using System.Text;

namespace PacketTool
{
    class RpcIdGenerator
    {
        public IReadOnlyDictionary<uint, string> IdToSign => _idToSign;

        private readonly Dictionary<string, uint> _signToId = new(); // 함수 서명으로 rpc id 검색
        private readonly Dictionary<uint, string> _idToSign = new(); // rpc id 로 함수서명 검색
        private uint _nextId = 1;

        public void Generate(IEnumerable<MethodInfo> methodInfos)
        {
            foreach (MethodInfo method in methodInfos)
            {
                string sign = CreateMethodSignature(method);
                var impl = method.GetCustomAttribute<RpcImplementationAttribute>();
                impl.Id = AssignId(sign);
            }
        }

        public uint AssignId(string methodSignature)
        {
            _signToId[methodSignature] = _nextId;
            _idToSign[_nextId] = methodSignature;
            return _nextId++;
        }

        /// <summary>
        /// DeclaringTypeName.MethodName.(ParameterTypeString1,ParameterTypeString2, ... ParameterTypeStringN)
        /// </summary>
        private string CreateMethodSignature(MethodInfo method)
        {
            StringBuilder sb = new StringBuilder();

            Type declaring = method.DeclaringType;
            sb.Append(declaring.Name);
            sb.Append('.');
            sb.Append(method.Name);
            sb.Append('.');
            sb.Append('(');

            ParameterInfo[] parameters = method.GetParameters();

            for (int i = 0; i < parameters.Length; i++)
            {
                sb.Append($"{TypeLookups.NameByType[parameters[i].ParameterType]}");

                if (i < parameters.Length - 1)
                {
                    sb.Append(',');
                    sb.Append(' ');
                }
            }

            sb.Append(')');
            return sb.ToString();
        }
    }
}
