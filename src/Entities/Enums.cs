namespace IZU.Entities
{
    public enum FunctionTypes
    {
        NONE,
        R,
        W,
    }
    public enum VariableTypes
    {
        //
        // 摘要:
        //     S7 Bit variable type (bool)
        Bool,
        //
        // 摘要:
        //     S7 Byte variable type (8 bits)
        Byte,
        //
        // 摘要:
        //     S7 Word variable type (16 bits, 2 bytes)
        Word,
        //
        // 摘要:
        //     S7 DWord variable type (32 bits, 4 bytes)
        DWord,
        //
        // 摘要:
        //     S7 Int variable type (16 bits, 2 bytes)
        Int,
        //
        // 摘要:
        //     DInt variable type (32 bits, 4 bytes)
        DInt,
        //
        // 摘要:
        //     Real variable type (32 bits, 4 bytes)
        Real,
        //
        // 摘要:
        //     LReal variable type (64 bits, 8 bytes)
        LReal,
        //
        // 摘要:
        //     Char Array / C-String variable type (variable)
        String,
        //
        // 摘要:
        //     S7 String variable type (variable)
        S7String,
        //
        // 摘要:
        //     S7 WString variable type (variable)
        S7WString,
        //
        // 摘要:
        //     Timer variable type
        Timer,
        //
        // 摘要:
        //     Counter variable type
        Counter,
        //
        // 摘要:
        //     DateTIme variable type
        DateTime,
        //
        // 摘要:
        //     DateTimeLong variable type
        DateTimeLong
    }


    public enum DeviceTypes
    {
        NONE,
        IZU,
        HID,
        AUTODOOR,
        FIREDOOR
    }
    public enum ActionTypes
    {
        NONE,
        HEARTBEAT,
        SENDBACK,
        START,
        STARTSIG,
        STOP,
        EMERG,
        RESET,
        POWEROFF,
        OPEN,
        OPENSIG,
        CLOSE,
        CLOSESIG,
        ONLINE,
        ONLINESTATE,
        MOPEN,
        MCLOSE,
        INITIAL,
        SWITCH
    }

    public enum TaskServiceStatus
    {
        NotStarted,
        Connecting,
        Connected
    }
}
