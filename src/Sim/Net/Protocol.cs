namespace PerformativeMail.Sim.Net;

public static class Protocol
{
    // Frozen U4.1 field list. FNV-1a 32 of:
    // U4.1:Hello=1,HelloOk=2,HelloReject=3,Input=10,Snapshot=20;Hello:u32;HelloOk:u32,u32;HelloReject:u8;InputCmd:u32,i8,i8,u16,u16;Input:u8,InputCmd[1..3];Snapshot:u32,u16,PlayerSnapshot[];PlayerSnapshot:u32,i32,i32,i32,u16,u8,u8,u32
    public const uint SchemaHash = 0x4112C9FA;

    public const uint ContentHash = 0;

    public const uint Hash = SchemaHash ^ ContentHash;
}
