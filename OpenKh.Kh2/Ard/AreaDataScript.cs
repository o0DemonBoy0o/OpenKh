using OpenKh.Common;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Xe.BinaryMapper;

namespace OpenKh.Kh2.Ard
{
    public interface IAreaDataCommand
    {
        void Parse(int nRow, List<string> tokens);
    }

    public interface IAreaDataSetting
    {
    }

    public enum Party : int
    {
        NO_FRIEND,
        DEFAULT,
        W_FRIEND,
        W_FRIEND_IN,
        W_FRIEND_FIX,
        W_FRIEND_ONLY,
        DONALD_ONLY,
    };

    public class SpawnScriptParserException : Exception
    {
        public SpawnScriptParserException(int line, string message) :
            base($"Line {line}: {message}")
        { }
    }

    public class SpawnScriptMissingProgramException : SpawnScriptParserException
    {
        public SpawnScriptMissingProgramException(int line) :
            base(line, "Expected \"Program\" statement.")
        { }
    }

    public class SpawnScriptCommandNotRecognizedException : SpawnScriptParserException
    {
        public SpawnScriptCommandNotRecognizedException(int line, string command) :
            base(line, $"The command \"{command}\" was not recognized.")
        { }
    }

    public class SpawnScriptNotANumberException : SpawnScriptParserException
    {
        public SpawnScriptNotANumberException(int line, string token) :
            base(line, $"Expected an integer value, but got '{token}'.")
        { }
    }

    public class SpawnScriptShortException : SpawnScriptParserException
    {
        public SpawnScriptShortException(int line, int value) :
            base(line, $"The value {value} is too big. Please chose a value between {0} and {short.MaxValue}.")
        { }
    }

    public class SpawnScriptStringTooLongException : SpawnScriptParserException
    {
        public SpawnScriptStringTooLongException(int line, string value, int maxLength) :
            base(line, $"The value \"{value}\" is {value.Length} characters long, but the maximum allowed is {maxLength}.")
        { }
    }

    public class SpawnScriptInvalidEnumException : SpawnScriptParserException
    {
        public SpawnScriptInvalidEnumException(int line, string token, IEnumerable<string> allowed) :
            base(line, $"The token '{token}' is not recognized as only {string.Join(",", allowed.Select(x => $"'{x}'"))} are allowed.")
        { }
    }

    public class AreaDataScript
    {
        private static readonly string[] PartyValues = new string[]
        {
            "NO_FRIEND",
            "DEFAULT",
            "W_FRIEND",
            "W_FRIEND_IN",
            "W_FRIEND_FIX",
            "W_FRIEND_ONLY",
            "DONALD_ONLY",
        };

        private enum LexState
        {
            Init,
            Code,
        }

        private const short Terminator = -1;
        private static readonly IBinaryMapping Mapping =
            MappingConfiguration.DefaultConfiguration()
                .ForType<string>(ScriptStringReader, ScriptStringWriter)
                .ForType<List<string>>(ScriptMultipleStringReader, ScriptMultipleStringWriter)
                .ForType<AreaSettings>(AreaSettingsReader, AreaSettingsWriter)
                .ForType<SetInventory>(SetInventoryReader, SetInventoryWriter)
                .Build();

        private static readonly Dictionary<int, Type> _idType = new Dictionary<int, Type>()
        {
            [0x00] = typeof(Spawn),
            [0x01] = typeof(MapOcclusion),
            [0x02] = typeof(MultipleSpawn),
            [0x03] = typeof(Unk03),
            [0x04] = typeof(Unk04),
            [0x05] = typeof(Unk05),
            [0x06] = typeof(Unk06),
            [0x07] = typeof(Unk07),
            [0x09] = typeof(Unk09),
            [0x0A] = typeof(Unk0a),
            [0x0B] = typeof(Unk0b),
            [0x0C] = typeof(AreaSettings),
            [0x0E] = typeof(Unk0e),
            [0x0F] = typeof(Party),
            [0x10] = typeof(Bgm),
            [0x11] = typeof(Unk11),
            [0x13] = typeof(Unk13),
            [0x14] = typeof(Unk14),
            [0x15] = typeof(Mission),
            [0x16] = typeof(Layout),
            [0x17] = typeof(Unk17),
            [0x18] = typeof(Unk18),
            [0x19] = typeof(Unk19),
            [0x1A] = typeof(Unk1a),
            [0x1B] = typeof(Unk1b),
            // 0x1C???
            [0x1D] = typeof(Unk1d),
            [0x1E] = typeof(BattleLevel),
            [0x1F] = typeof(Unk1f),
        };

        private static readonly Dictionary<int, Type> _idSetType = new Dictionary<int, Type>()
        {
            [0x00] = typeof(SetEvent),
            [0x01] = typeof(SetJump),
            [0x02] = typeof(SetProgressFlag),
            [0x03] = typeof(SetMenuFlag),
            [0x04] = typeof(SetMember),
            [0x05] = typeof(SetUnk05),
            [0x06] = typeof(SetInventory),
            [0x07] = typeof(SetPartyMenu),
            [0x08] = typeof(SetUnkFlag),
        };

        private static readonly Dictionary<Type, int> _typeId =
            _idType.ToDictionary(x => x.Value, x => x.Key);

        private static readonly Dictionary<string, Type> _typeStr =
            _idType.ToDictionary(x => x.Value.Name, x => x.Value);

        private static readonly Dictionary<Type, int> _typeSetId =
            _idSetType.ToDictionary(x => x.Value, x => x.Key);

        private class Header
        {
            [Data] public short Id { get; set; }
            [Data] public short Length { get; set; }
        }

        public class Spawn : IAreaDataCommand
        {
            [Data(Count = 4)] public string SpawnSet { get; set; }

            public void Parse(int nRow, List<string> tokens)
            {
                SpawnSet = ParseAsString(nRow, GetToken(nRow, tokens, 1), 4);
            }

            public override string ToString() =>
                $"{nameof(Spawn)} \"{SpawnSet}\"";
        }

        public class MapOcclusion : IAreaDataCommand
        {
            [Data] public int Flags1 { get; set; }
            [Data] public int Flags2 { get; set; }

            public void Parse(int nRow, List<string> tokens)
            {
                Flags1 = ParseAsInt(nRow, GetToken(nRow, tokens, 1));
                Flags2 = ParseAsInt(nRow, GetToken(nRow, tokens, 2));
            }

            public override string ToString() =>
                $"{nameof(MapOcclusion)} 0x{Flags1:x08} 0x{Flags2:x08}";
        }

        public class MultipleSpawn : IAreaDataCommand
        {
            [Data] public List<string> SpawnSet { get; set; }

            public void Parse(int nRow, List<string> tokens)
            {
                SpawnSet = Enumerable
                    .Range(1, tokens.Count - 1)
                    .Select(i => ParseAsString(nRow, GetToken(nRow, tokens, i), 4))
                    .ToList();
            }

            public override string ToString() =>
                $"{nameof(MultipleSpawn)} {string.Join(" ", SpawnSet.Select(x => $"\"{x}\""))}";
        }

        public class Unk03 : IAreaDataCommand
        {
            [Data] public int Unk00 { get; set; }
            [Data(Count = 4)] public string SpawnSet { get; set; }

            public void Parse(int nRow, List<string> tokens)
            {
                Unk00 = ParseAsInt(nRow, GetToken(nRow, tokens, 1));
                SpawnSet = ParseAsString(nRow, GetToken(nRow, tokens, 2), 4);
            }

            public override string ToString() =>
                $"{nameof(Unk03)} {Unk00} \"{SpawnSet}\"";
        }

        public class Unk04 : IAreaDataCommand
        {
            [Data] public int Value { get; set; }

            public void Parse(int nRow, List<string> tokens)
            {
                Value = ParseAsInt(nRow, GetToken(nRow, tokens, 1));
            }

            public override string ToString() =>
                $"{nameof(Unk04)} {Value}";
        }

        public class Unk05 : IAreaDataCommand
        {
            [Data] public int Value { get; set; }

            public override string ToString() =>
                $"{nameof(Unk05)} {Value}";

            public void Parse(int nRow, List<string> tokens)
            {
                Value = ParseAsInt(nRow, GetToken(nRow, tokens, 1));
            }
        }

        public class Unk06 : IAreaDataCommand
        {
            [Data] public int Value { get; set; }

            public void Parse(int nRow, List<string> tokens)
            {
                Value = ParseAsInt(nRow, GetToken(nRow, tokens, 1));
            }

            public override string ToString() =>
                $"{nameof(Unk06)} {Value}";
        }

        public class Unk07 : IAreaDataCommand
        {
            [Data] public int Value { get; set; }

            public void Parse(int nRow, List<string> tokens)
            {
                Value = ParseAsInt(nRow, GetToken(nRow, tokens, 1));
            }

            public override string ToString() =>
                $"{nameof(Unk07)} {Value}";
        }

        public class Unk09 : IAreaDataCommand
        {
            [Data(Count = 4)] public string Value { get; set; }

            public void Parse(int nRow, List<string> tokens)
            {
                Value = ParseAsString(nRow, GetToken(nRow, tokens, 1), 4);
            }

            public override string ToString() =>
                $"{nameof(Unk09)} \"{Value}\"";
        }

        public class Unk0a : IAreaDataCommand
        {
            [Data] public int Value { get; set; }

            public void Parse(int nRow, List<string> tokens)
            {
                Value = ParseAsInt(nRow, GetToken(nRow, tokens, 1));
            }

            public override string ToString() =>
                $"{nameof(Unk0a)} {Value}";
        }

        public class Unk0b : IAreaDataCommand
        {
            [Data] public int Value1 { get; set; }
            [Data] public int Value2 { get; set; }

            public void Parse(int nRow, List<string> tokens)
            {
                Value1 = ParseAsInt(nRow, GetToken(nRow, tokens, 1));
                Value2 = ParseAsInt(nRow, GetToken(nRow, tokens, 2));
            }

            public override string ToString() =>
                $"{nameof(Unk0b)} 0x{Value1:x} 0x{Value2:x}";
        }

        public class AreaSettings : IAreaDataCommand
        {
            public short Unk00 { get; set; }
            public short Unk02 { get; set; }
            public List<IAreaDataSetting> Settings { get; set; }

            public void Parse(int nRow, List<string> tokens)
            {
                throw new NotImplementedException();
            }

            public override string ToString()
            {
                var sb = new StringBuilder();
                sb.AppendLine($"{nameof(AreaSettings)} {Unk00:x} 0x{Unk02:x}");
                foreach (var item in Settings)
                    sb.AppendLine($"\t{item}");

                return sb.ToString().Replace("\r", string.Empty);
            }
        }

        public class Unk0e : IAreaDataCommand
        {
            [Data] public int Value { get; set; }

            public void Parse(int nRow, List<string> tokens)
            {
                Value = ParseAsInt(nRow, GetToken(nRow, tokens, 1));
            }

            public override string ToString() =>
                $"{nameof(Unk0e)} {Value}";
        }

        public class Party : IAreaDataCommand
        {
            [Data] public Ard.Party Value { get; set; }

            public void Parse(int nRow, List<string> tokens)
            {
                Value = (Ard.Party)ParseAsEnum(nRow, GetToken(nRow, tokens, 1), PartyValues);
            }

            public override string ToString() =>
                $"{nameof(Party)} {Value}";
        }

        public class Bgm : IAreaDataCommand
        {
            [Data] public short BgmField { get; set; }
            [Data] public short BgmBattle { get; set; }

            public void Parse(int nRow, List<string> tokens)
            {
                var token1 = GetToken(nRow, tokens, 1);
                var token2 = GetToken(nRow, tokens, 2);
                BgmField = token1 == "Default" ? (short)0 : ParseAsShort(nRow, token1);
                BgmBattle = token2 == "Default" ? (short)0 : ParseAsShort(nRow, token2);
            }

            public override string ToString()
            {
                var sb = new StringBuilder();
                sb.Append($"{nameof(Bgm)} ");
                sb.Append(BgmField == 0 ? "Default" : BgmField.ToString());
                sb.Append(" ");
                sb.Append(BgmBattle == 0 ? "Default" : BgmBattle.ToString());

                return sb.ToString();
            }
        }

        public class Unk11 : IAreaDataCommand
        {
            [Data] public int Value { get; set; }

            public void Parse(int nRow, List<string> tokens)
            {
                Value = ParseAsInt(nRow, GetToken(nRow, tokens, 1));
            }

            public override string ToString() =>
                $"{nameof(Unk11)} 0x{Value:x}";
        }

        public class Unk13 : IAreaDataCommand
        {
            [Data] public int Value { get; set; }

            public void Parse(int nRow, List<string> tokens)
            {
                Value = ParseAsInt(nRow, GetToken(nRow, tokens, 1));
            }

            public override string ToString() =>
                $"{nameof(Unk13)} {Value}";
        }

        public class Unk14 : IAreaDataCommand
        {
            public void Parse(int nRow, List<string> tokens) { }

            public override string ToString() => nameof(Unk14);
        }

        public class Mission : IAreaDataCommand
        {
            [Data] public int Unk00 { get; set; }
            [Data] public string Value { get; set; }

            public void Parse(int nRow, List<string> tokens)
            {
                Unk00 = ParseAsInt(nRow, GetToken(nRow, tokens, 1));
                Value = ParseAsString(nRow, GetToken(nRow, tokens, 2), 32);
            }

            public override string ToString() =>
                $"{nameof(Mission)} 0x{Unk00:X} \"{Value}\"";
        }

        public class Layout : IAreaDataCommand
        {
            [Data] public string Value { get; set; }

            public void Parse(int nRow, List<string> tokens)
            {
                Value = ParseAsString(nRow, GetToken(nRow, tokens, 1), 32);
            }

            public override string ToString() =>
                $"{nameof(Layout)} \"{Value}\"";
        }

        public class Unk17 : IAreaDataCommand
        {
            public void Parse(int nRow, List<string> tokens) { }

            public override string ToString() => nameof(Unk17);
        }

        public class Unk18 : IAreaDataCommand
        {
            [Data] public int Value1 { get; set; }
            [Data] public int Value2 { get; set; }

            public void Parse(int nRow, List<string> tokens)
            {
                Value1 = ParseAsInt(nRow, GetToken(nRow, tokens, 1));
                Value2 = ParseAsInt(nRow, GetToken(nRow, tokens, 2));
            }

            public override string ToString() =>
                $"{nameof(Unk18)} 0x{Value1:x} 0x{Value2:x}";
        }

        public class Unk19 : IAreaDataCommand
        {
            [Data] public int Value { get; set; }

            public void Parse(int nRow, List<string> tokens)
            {
                Value = ParseAsInt(nRow, GetToken(nRow, tokens, 1));
            }

            public override string ToString() =>
                $"{nameof(Unk19)} 0x{Value:x}";
        }

        public class Unk1a : IAreaDataCommand
        {
            [Data] public int Value { get; set; }

            public void Parse(int nRow, List<string> tokens)
            {
                Value = ParseAsInt(nRow, GetToken(nRow, tokens, 1));
            }

            public override string ToString() =>
                $"{nameof(Unk1a)} {Value}";
        }

        public class Unk1b : IAreaDataCommand
        {
            [Data] public int Value { get; set; }

            public void Parse(int nRow, List<string> tokens)
            {
                Value = ParseAsInt(nRow, GetToken(nRow, tokens, 1));
            }

            public override string ToString() =>
                $"{nameof(Unk1b)} {Value}";
        }

        public class Unk1d : IAreaDataCommand
        {
            [Data] public int Value { get; set; }

            public void Parse(int nRow, List<string> tokens)
            {
                Value = ParseAsInt(nRow, GetToken(nRow, tokens, 1));
            }

            public override string ToString() =>
                $"{nameof(Unk1d)} {Value}";
        }

        public class BattleLevel : IAreaDataCommand
        {
            [Data] public int Value { get; set; }

            public void Parse(int nRow, List<string> tokens)
            {
                Value = ParseAsInt(nRow, GetToken(nRow, tokens, 1));
            }

            public override string ToString() =>
                $"{nameof(BattleLevel)} {Value}";
        }

        public class Unk1f : IAreaDataCommand
        {
            [Data(Count = 4)] public string Value { get; set; }

            public void Parse(int nRow, List<string> tokens)
            {
                Value = ParseAsString(nRow, GetToken(nRow, tokens, 1), 4);
            }

            public override string ToString() =>
                $"{nameof(Unk1f)} \"{Value}\"";
        }

        public class SetEvent : IAreaDataSetting
        {
            [Data] public short Type { get; set; }
            [Data] public string Value { get; set; }

            public override string ToString() =>
                $"{nameof(SetEvent)} \"{Value}\" Type {Type}";
        }

        public class SetJump : IAreaDataSetting
        {
            [Data] public short Padding { get; set; }
            [Data] public short Type { get; set; }
            [Data] public short World { get; set; }
            [Data] public short Area { get; set; }
            [Data] public short Entrance { get; set; }
            [Data] public short LocalSet { get; set; }
            [Data] public short FadeType { get; set; }

            public override string ToString()
            {
                var items = new List<string>();
                items.Add(nameof(SetJump));
                items.Add(nameof(Type));
                items.Add(Type.ToString());
                items.Add(nameof(World));
                items.Add(Constants.WorldIds[World].ToUpper());
                items.Add(nameof(Area));
                items.Add(Area.ToString());
                items.Add(nameof(Entrance));
                items.Add(Entrance.ToString());
                items.Add(nameof(LocalSet));
                items.Add(LocalSet.ToString());
                items.Add(nameof(FadeType));
                items.Add(FadeType.ToString());

                return string.Join(" ", items);
            }
        }

        public class SetProgressFlag : IAreaDataSetting
        {
            [Data] public short Value { get; set; }

            public override string ToString() =>
                $"{nameof(SetProgressFlag)} 0x{Value:X}";
        }

        public class SetMenuFlag : IAreaDataSetting
        {
            [Data] public short Value { get; set; }

            public override string ToString() =>
                $"{nameof(SetMenuFlag)} 0x{Value:X}";
        }

        public class SetMember : IAreaDataSetting
        {
            [Data] public short Value { get; set; }

            public override string ToString() =>
                $"{nameof(SetMember)} {Value}";
        }

        public class SetUnk05 : IAreaDataSetting
        {
            [Data] public short Value { get; set; }

            public override string ToString() =>
                $"{nameof(SetUnk05)} 0x{Value:X}";
        }

        public class SetInventory : IAreaDataSetting
        {
            public List<int> Items { get; set; }

            public override string ToString() =>
                $"{nameof(SetInventory)} {string.Join(" ", Items)}";
        }

        public class SetPartyMenu : IAreaDataSetting
        {
            [Data] public short Value { get; set; }

            public override string ToString() =>
                $"{nameof(SetPartyMenu)} {Value}";
        }

        public class SetUnkFlag : IAreaDataSetting
        {
            public override string ToString() =>
                $"{nameof(SetUnkFlag)}";
        }

        public short ProgramId { get; set; }
        public List<IAreaDataCommand> Functions { get; set; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Program 0x{ProgramId:X02}");
            sb.AppendJoin("\n", Functions);

            return sb.ToString().Replace("\r", string.Empty);
        }

        public static List<AreaDataScript> Read(Stream stream) =>
            ReadAll(stream).ToList();

        public static IEnumerable<IAreaDataCommand> Read(Stream stream, int programId)
        {
            while (true)
            {
                var header = BinaryMapping.ReadObject<Header>(stream);
                if (header.Id == Terminator && header.Length == 0)
                    return null;

                if (header.Id == programId)
                    return ParseScript(stream.ReadBytes(header.Length - 4));
                else
                    stream.Position += header.Length - 4;
            }
        }

        public static void Write(Stream stream, IEnumerable<AreaDataScript> scripts)
        {
            foreach (var script in scripts)
            {
                using var dataStream = new MemoryStream();
                Write(dataStream, script.Functions);

                stream.Write(script.ProgramId);
                stream.Write((short)(dataStream.Length + 4));
                dataStream.SetPosition(0).CopyTo(stream);
            }
            stream.Write(0x0000FFFF);
        }

        public static string Decompile(IEnumerable<AreaDataScript> scripts) =>
            string.Join("\n", scripts.Select(x => x.ToString()));

        public static IEnumerable<AreaDataScript> Compile(string text)
        {
            const char Comment = '#';
            AreaDataScript newScript() => new AreaDataScript
            {
                Functions = new List<IAreaDataCommand>()
            };

            var script = newScript();
            var state = LexState.Init;
            var lines = text
                .Replace("\n\r", "\n")
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Split('\n');
            var row = 0;

            foreach (var line in lines)
            {
                row++;
                var cleanLine = line.Split(Comment);
                var tokens = Tokenize(row, cleanLine[0]).ToList();
                if (tokens.Count == 0)
                    continue;

                if (state == LexState.Init && tokens[0] != "Program")
                    throw new SpawnScriptMissingProgramException(row);

                switch (tokens[0])
                {
                    case "Program":
                        var programId = ParseAsShort(row, GetToken(row, tokens, 1));
                        if (state == LexState.Code)
                        {
                            yield return script;
                            script = newScript();
                        }

                        state = LexState.Code;
                        script.ProgramId = programId;
                        break;
                    default:
                        script.Functions.Add(ParseCommand(row, tokens));
                        break;
                }
            }

            yield return script;
        }

        private static IEnumerable<AreaDataScript> ReadAll(Stream stream)
        {
            while (true)
            {
                var header = BinaryMapping.ReadObject<Header>(stream);
                if (header.Id == Terminator && header.Length == 0)
                    yield break;

                var bytecode = stream.ReadBytes(header.Length - 4);
                yield return new AreaDataScript()
                {
                    ProgramId = header.Id,
                    Functions = ParseScript(bytecode).ToList()
                };
            }
        }

        private static IEnumerable<IAreaDataCommand> ParseScript(byte[] bytecode)
        {
            for (int pc = 0; pc < bytecode.Length;)
            {
                var opcode = BitConverter.ToUInt16(bytecode, pc);
                var parameterCount = BitConverter.ToUInt16(bytecode, pc + 2);
                using var stream = new MemoryStream(bytecode, pc + 4, parameterCount * 4);
                var instance = Activator.CreateInstance(_idType[opcode]);
                yield return Mapping.ReadObject(stream, instance) as IAreaDataCommand;

                pc += 4 + parameterCount * 4;
            }
        }

        private static void Write(Stream stream, IEnumerable<IAreaDataCommand> commands)
        {
            foreach (var command in commands)
            {
                using var commandStream = new MemoryStream();
                Mapping.WriteObject(commandStream, command);
                var opcode = (short)_typeId[command.GetType()];
                var parameterCount = (short)(commandStream.Length / 4);

                stream.Write(opcode);
                stream.Write(parameterCount);
                commandStream.SetPosition(0).CopyTo(stream);
            }
        }

        private static object ScriptStringReader(MappingReadArgs args)
        {
            if (args.Count > 1)
            {
                var data = args.Reader.ReadBytes(args.Count);
                int byteCount;
                for (byteCount = 0; byteCount < data.Length; byteCount++)
                    if (data[byteCount] == 0)
                        break;

                return Encoding.UTF8.GetString(data, 0, byteCount);
            }
            else
            {
                var sb = new StringBuilder();
                while (true)
                {
                    var ch = args.Reader.ReadByte();
                    if (ch == 0 || ch < 0)
                        break;

                    sb.Append((char)ch);
                }

                return sb.ToString();
            }
        }

        private static void ScriptStringWriter(MappingWriteArgs args)
        {
            var value = (string)args.Item;
            var data = new byte[4];
            if (value.Length > 2)
            {
                data[0] = (byte)value[0];
                data[1] = (byte)value[1];
                data[2] = (byte)value[2];
                if (value.Length >= 4)
                    data[3] = (byte)value[3];
            }
            else if (value.Length > 0)
            {
                data[0] = (byte)value[0];
                if (value.Length == 2)
                    data[1] = (byte)value[1];
            }

            args.Writer.BaseStream.Write(data);
        }

        private static object ScriptMultipleStringReader(MappingReadArgs args)
        {
            var list = new List<string>();
            args.Count = 4;
            while (args.Reader.BaseStream.Position + 3 < args.Reader.BaseStream.Length)
                list.Add(ScriptStringReader(args) as string);

            return list;
        }

        private static void ScriptMultipleStringWriter(MappingWriteArgs args)
        {
            var items = (List<string>)args.Item;
            foreach (var item in items)
            {
                args.Item = item;
                ScriptStringWriter(args);
            }
        }

        private static object AreaSettingsReader(MappingReadArgs args)
        {
            var reader = args.Reader;
            var settings = new AreaSettings
            {
                Unk00 = reader.ReadInt16(),
                Unk02 = reader.ReadInt16(),
                Settings = new List<IAreaDataSetting>()
            };

            while (reader.BaseStream.Position < args.Reader.BaseStream.Length)
            {
                var opcode = reader.ReadInt16();
                var instance = Activator.CreateInstance(_idSetType[opcode]);
                instance = Mapping.ReadObject(reader.BaseStream, instance);
                settings.Settings.Add(instance as IAreaDataSetting);
            }

            return settings;
        }

        private static void AreaSettingsWriter(MappingWriteArgs args)
        {
            // TODO
        }

        private static object SetInventoryReader(MappingReadArgs args)
        {
            var count = args.Reader.ReadInt16();
            return new SetInventory
            {
                Items = Enumerable
                    .Range(0, count)
                    .Select(_ => args.Reader.ReadInt32())
                    .ToList()
            };
        }

        private static void SetInventoryWriter(MappingWriteArgs args)
        {
            // TODO
        }

        private static IAreaDataCommand ParseCommand(int nRow, List<string> tokens)
        {
            var commandName = tokens[0];
            if (!_typeStr.TryGetValue(commandName, out var type))
                throw new SpawnScriptCommandNotRecognizedException(nRow, commandName);

            var command = Activator.CreateInstance(type) as IAreaDataCommand;
            command.Parse(nRow, tokens);
            return command;
        }

        private static IEnumerable<string> Tokenize(int row, string line)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < line.Length; i++)
            {
                var ch = line[i];
                if (char.IsWhiteSpace(ch))
                {
                    if (sb.Length > 0)
                        yield return sb.ToString();
                    sb.Clear();
                    continue;
                }
                else if (ch == '"')
                {
                    sb.Append(ch);
                    do
                    {
                        i++;
                        if (i >= line.Length)
                            throw new SpawnScriptParserException(row, $"Missing '\"'.");
                        sb.Append(ch = line[i]);
                    } while (ch != '"');
                }
                else
                    sb.Append(ch);
            }

            if (sb.Length > 0)
                yield return sb.ToString();
        }

        private static string GetToken(int row, List<string> tokens, int tokenIndex) =>
            tokens[tokenIndex];

        public static string ParseAsString(int row, string text, int maxLength)
        {
            if (text.Length < 2)
                throw new SpawnScriptParserException(row, $"Expected a string but got '{text}'");
            if (text[0] != '"' || text[text.Length - 1] != '"')
                throw new SpawnScriptParserException(row, $"Expected a string but got '{text}' with probably wrong double-quotes.");

            text = text.Substring(1, text.Length - 2);
            if (text.Length > maxLength)
                throw new SpawnScriptStringTooLongException(row, text, maxLength);

            return text;
        }

        private static short ParseAsShort(int row, string text)
        {
            var value = ParseAsInt(row, text);
            if (value < 0 || value > short.MaxValue)
                throw new SpawnScriptShortException(row, value);
            return (short)value;
        }

        private static int ParseAsInt(int row, string token)
        {
            var isHexadecimal = token.Length > 2 &&
                token[0] == '0' && token[1] == 'x';
            var nStyle = isHexadecimal ? NumberStyles.HexNumber : NumberStyles.Integer;
            var strValue = isHexadecimal ? token.Substring(2) : token;

            if (!int.TryParse(strValue, nStyle, null, out var value))
                throw new SpawnScriptNotANumberException(row, token);
            return value;
        }

        private static int ParseAsEnum(int row, string token, string[] allowValues)
        {
            for (var i = 0; i < allowValues.Length; i++)
            {
                if (token == allowValues[i])
                    return i;
            }

            throw new SpawnScriptInvalidEnumException(row, token, allowValues);
        }
    }
}