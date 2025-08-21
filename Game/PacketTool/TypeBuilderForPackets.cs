using System.Text;

namespace PacketTool
{
    internal static class TypeBuilderForPackets
    {
        internal static void Build(string defPath, string outDir)
        {
            // 1. packets.def 파일 파싱
            List<PacketDef> packetDefs = File.ReadAllLines(defPath) // 전체 라인 읽음
                                             .Select(l => l.Trim()) // 각 라인 앞뒤 공백 없앰
                                             .Where(l => l.Length > 0 && !l.StartsWith('#')) // 라인에 내용이 없거나 주석이면 제외
                                             .Select(Parse) // 유효한 라인들에 대해서 PacketDef DTO 로 파싱
                                             .ToList();

            // 2. PacketId enum 타입 정의 .cs 파일 저장
            string enumText = Build_PacketIdEnum(packetDefs);
            File.WriteAllText(Path.Combine(outDir, "PacketId.cs"), enumText, Encoding.UTF8);

            // 3. PacketInterface 타입 정의 .cs 파일 저장 (이렇게 하는거보다는 정의미리 해놓고 enum 타입 파일만 수정하는형태가 더 나음)
            string interfaceText = Build_PacketInterface();
            File.WriteAllText(Path.Combine(outDir, "IPacket.cs"), interfaceText, Encoding.UTF8);

            // 4. Packet class 전부 정의 .cs 파일 저장
            string classesText = Build_PacketClasses(packetDefs);
            File.WriteAllText(Path.Combine(outDir, "Packets.cs"), classesText, Encoding.UTF8);

            // 5. Packet Factory 정의 .cs 파일 저장
            string factoryClassText = Build_PacketFactoryClass(packetDefs);
            File.WriteAllText(Path.Combine(outDir, "PacketFactory.cs"), factoryClassText, Encoding.UTF8);
        }

        /// <summary>
        /// TODO : 이거 로직 성능개선해보기
        /// </summary>
        internal static PacketDef Parse(string line)
        {
            string[] splits = line.Split(new[] { ' ', '\t' }, 3, StringSplitOptions.RemoveEmptyEntries);

            foreach (var item in splits)
            {
                Console.WriteLine(item);
            }

            string idHex = splits[0];
            string name = splits[1];
            List<FieldDef> fields =
                splits[2].Split(',', StringSplitOptions.RemoveEmptyEntries)
                         .Select(l =>
                         {
                             string[] pair = l.Trim().Split(' ');
                             return new FieldDef(pair[1], pair[0]);
                         })
                         .ToList();

            return new PacketDef(name, idHex, fields);
        }


        internal static string Build_PacketInterface()
        {
            return """
                using System.IO;

                namespace Game.NetworkContracts
                {
                    public interface IPacket
                    {
                        PacketId PacketId { get; }

                        void Serialize(BinaryWriter writer);
                        void Deserialize(BinaryReader reader);
                    }
                }
                
                """;
        }

        internal static string Build_PacketIdEnum(IEnumerable<PacketDef> packetDefs)
        {
            StringBuilder sb = new StringBuilder("""
                using System;
                using System.Collections.Generic;
                using System.IO;
                using System.Linq;
                using System.Net.Http;
                using System.Threading;
                using System.Threading.Tasks;

                namespace Game.NetworkContracts
                {    
                    public enum PacketId : ushort    
                    {    
                """);

            sb.AppendLine();

            foreach (var packetDef in packetDefs)
            {
                sb.AppendLine($"        {packetDef.Name} = {packetDef.IdHex},");
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        internal static string Build_PacketClasses(IEnumerable<PacketDef> packetDefs)
        {
            StringBuilder sb = new StringBuilder("""
                using System;
                using System.Collections.Generic;
                using System.IO;
                using System.Linq;
                using System.Net.Http;
                using System.Threading;
                using System.Threading.Tasks;

                namespace Game.NetworkContracts
                {
                """);

            foreach (var packetDef in packetDefs)
            {
                sb.AppendLine(Build_PacketClass(packetDef));
            }

            sb.Append('}');

            return sb.ToString();
        }

        internal static string Build_PacketClass(PacketDef packetDef)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine();
            sb.AppendLine($"    public sealed class {packetDef.Name} : IPacket");
            sb.AppendLine("    {");
            sb.AppendLine($"        public PacketId PacketId => PacketId.{packetDef.Name};");
            sb.AppendLine();

            // fields
            //-------------------------------------------------
            foreach (var fieldDef in packetDef.Fields)
            {
                sb.AppendLine($"        public {fieldDef.CsType} {fieldDef.Name} {{ get; set; }}");
            }

            // Serialize()
            //-------------------------------------------------
            sb.AppendLine();
            sb.AppendLine("        public void Serialize(BinaryWriter writer)");
            sb.AppendLine("        {");

            foreach (var fieldDef in packetDef.Fields)
            {
                sb.AppendLine($"            writer.Write({fieldDef.Name});");
            }

            sb.AppendLine("        }");

            // Deserialize()
            //-------------------------------------------------
            sb.AppendLine();
            sb.AppendLine("        public void Deserialize(BinaryReader reader)");
            sb.AppendLine("        {");


            foreach (var fieldDef in packetDef.Fields)
            {
                sb.AppendLine($"            {fieldDef.Name} = reader.Read{TypeLookups.TypeByName[fieldDef.CsType].Name}();");
            }

            sb.AppendLine("        }");

            sb.Append("    }");
            return sb.ToString();
        }

        internal static string Build_PacketFactoryClass(IEnumerable<PacketDef> packetDefs)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("""
                using System;
                using System.Collections.Generic;
                using System.IO;
                using System.Linq;
                using System.Net.Http;
                using System.Threading;
                using System.Threading.Tasks;

                namespace Game.NetworkContracts
                {
                    public static class PacketFactory
                    {
                """);
            sb.AppendLine();
            sb.AppendLine("        // Constructors table");
            sb.AppendLine("        private static readonly Dictionary<PacketId, Func<IPacket>> _constructors = new Dictionary<PacketId, Func<IPacket>>");
            sb.AppendLine("        {");

            foreach (var packetDef in packetDefs)
            {
                sb.AppendLine($"            {{ PacketId.{packetDef.Name}, () => new {packetDef.Name}() }},");
            }

            sb.AppendLine("        };");

            sb.AppendLine();
            sb.AppendLine("""
                        /// <summary>
                        /// 데이터 송신시 원하는 패킷객체를 Serialize 하기위함
                        /// </summary>
                        /// <param name="packet"> 송신대상</param>
                        /// <returns> Serialized </returns>
                """);
            sb.AppendLine("""
                        public static byte[] ToBytes(IPacket packet)
                        {
                            using (MemoryStream stream = new MemoryStream())
                            using (BinaryWriter writer = new BinaryWriter(stream))
                            {
                                writer.Write((ushort)packet.PacketId);
                                packet.Serialize(writer);

                                return stream.ToArray();
                            }
                        }
                """);

            sb.AppendLine();
            sb.AppendLine("""
                        /// <summary>
                        /// 수신한 데이터를 Deserialize 하기위함
                        /// </summary>
                        /// <param name="body"> 수신한 데이터 </param>
                        /// <returns> Deserialized </returns>
                """);
            sb.AppendLine("""
                        public static IPacket FromBytes(byte[] body)
                        {
                            // Packet Id 도 못읽으면 잘못된 데이터임
                            if (body.Length < sizeof(PacketId))
                                return null;

                            PacketId packetId = (PacketId)BitConverter.ToUInt16(body, 0);

                            // 유효한 Packet Id 인지
                            if (_constructors.TryGetValue(packetId, out Func<IPacket> constructor) == false)
                                return null;

                            IPacket packet = constructor.Invoke();

                            // packetId 만큼을 제외한 순수 데이터만 스트림에 취급
                            using (MemoryStream stream = new MemoryStream(body, sizeof(ushort), body.Length - sizeof(ushort)))
                            using (BinaryReader reader = new BinaryReader(stream))
                            {
                                packet.Deserialize(reader);

                                return packet;
                            }
                        }
                """);

            sb.AppendLine("    }");
            sb.Append("}");

            return sb.ToString();
        }

        internal record FieldDef(string Name, string CsType);
        internal record PacketDef(string Name, string IdHex, List<FieldDef> Fields);
    }
}
