namespace Lizard.Core.Unwind;

public class WatcomUnwinder : IStackUnwinder
{
    /*
    PUSH 0x48 <-- max stack size (ignore)
    CALL StackGuard <-- assume stack size check function (ignore)
    PUSH EBX <-- stash
    PUSH ECX <-- stash
    PUSH ESI <-- stash
    PUSH EDI <-- stash
    PUSH EBP <-- last frame pointer
    MOV  EBP, ESP <-- new frame pointer
    SUB  ESP, 0x2c <-- space for locals
    MOV  dword ptr [EBP + local_2c], EAX <-- stash parameters
    MOV  dword ptr [EBP + local_28], EDX <-- stash parameters
    ...function body...
    MOV ESP, EBP
    POP EBP
    POP EDI
    POP ESI
    POP ECX
    POP EBX
    RET

    Calling convention:
        EAX, EDX, EBX, ECX, stack

    Prologue:
        PUSH imm
        CALL const
        PUSH reg32
        PUSH EBP
        MOV EBP, ESP
        SUB ESP, imm
        MOV [EBP + n], reg32

    Epilogue:
        MOV ESP, EBP
        POP EBP
        POP reg32
        RET

    StackGuard:
      XCHG dword ptr [ESP + param_4], EAX
      CALL StackGuard2
      MOV  EAX, dword ptr [ESP + param_4]
      RET  0x4

    StackGuard2:
      CMP  EAX, ESP
      JNC  l1
      SUB  EAX, ESP
      NEG  EAX
      CMP  EAX, dword ptr [PTR_DAT_00010354]
      JBE  l1
      RET
    l1:
      MOV  AX, SS
      CMP  AX, word ptr [DAT_0000fc48]
      JZ   l2
      RET
    l2:
      MOV  EAX, 0xfc4a
      MOV  EDX, 0x1
      CALL RaiseError
      PUSH EBX
      PUSH ECX
      PUSH ESI
      MOV  CL, DL
      MOV  EDX, dword ptr [EAX]
      MOV  EBX, dword ptr [EAX + 0x10]
      CMP  EBX, dword ptr [EDX + 0x4]
      JNC  l3
      MOV  EBX, dword ptr [EDX]
      LEA  ESI, [EBX + 0x1]
      MOV  dword ptr [EDX], ESI
      MOV  byte ptr [EBX], CL
      INC  dword ptr [EAX + 0x10]
    l3:
      POP  ESI
      POP  ECX
      POP  EBX
      RET

    push ebp
    mov ebp, esp
    ...
    mov esp, ebp
    pop ebp
    ret
    */

    public StackFrame? TryUnwindFrame(UnwinderContext context, StackFrame currentFrame)
    {
        return null;
    }
}
