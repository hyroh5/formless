using System.Linq;
using UnityEngine;

public class PhaseDirector : MonoBehaviour
{
    [Header("필수 레퍼런스")]
    public PhaseToSolidRealtime toSolid;         // 액/기 → 고체 (자동 분기)
    public SolidToLiquid2D      solidToLiquid;   // 고체 → 액체
    public SolidToGasConverter  solidToGas;      // 고체 → 기체
    public LiquidGasSwitcher2D  liquidGas;       // 액체 ↔ 기체 (LiquidGasSwitcher2D)

    [Header("Key Bindings")]
    public KeyCode keyToSolid  = KeyCode.Alpha1; // 1
    public KeyCode keyToLiquid = KeyCode.Alpha2; // 2
    public KeyCode keyToGas    = KeyCode.Alpha3; // 3

    // 고체 판단용
    Renderer[] solidRenderers;
    Rigidbody2D solidRb;
    Collider2D  solidCol;

    void Awake()
    {
        // 고체(자기 자신)에 붙은 렌더러/리짓/콜라이더 캐시
        solidRenderers = GetComponentsInChildren<Renderer>(true);
        solidRb = GetComponent<Rigidbody2D>();
        solidCol = GetComponent<Collider2D>();

        // 내부 핫키 OFF: 디렉터만 키를 받게 만든다
        if (solidToGas != null)    solidToGas.listenHotkey = false;
        if (liquidGas != null)     liquidGas.listenHotkeys = false;
        if (solidToLiquid != null) solidToLiquid.morphKey  = KeyCode.None;
    }

    void Update()
    {
        if (Input.GetKeyDown(keyToSolid))
        {
            // 1: 액체/기체 → 고체 (자동 분기)
            if (toSolid != null)
                toSolid.TriggerToSolid();
        }
        else if (Input.GetKeyDown(keyToLiquid))
        {
            // 2: 고체 → 액체, 아니면 기체 → 액체
            if (IsSolidVisible())
            {
                if (solidToLiquid != null) solidToLiquid.SendMessage("TransformToLiquid", SendMessageOptions.DontRequireReceiver);
                // 상태 동기화: 액체 세트를 쓴다면 LiquidGasSwitcher에도 Liquid로 고정
                if (liquidGas != null) liquidGas.ForceSetToLiquid();
            }
            else
            {
                if (liquidGas != null)
                {
                    if (liquidGas.current == LiquidGasSwitcher2D.Phase.Gas)
                        liquidGas.Switch_GasToLiquid();  // Gas→Liquid
                    // 이미 Liquid면 아무 것도 하지 않음(중복 방지)
                }
            }
        }
        else if (Input.GetKeyDown(keyToGas))
        {
            // 3: 고체 → 기체, 아니면 액체 → 기체
            if (IsSolidVisible())
            {
                if (solidToGas != null) solidToGas.ConvertToGas();
                if (liquidGas != null)  liquidGas.ForceSetToGas(); // 상태 동기화(가스 세트 사용 시)
            }
            else
            {
                if (liquidGas != null)
                {
                    if (liquidGas.current == LiquidGasSwitcher2D.Phase.Liquid)
                        liquidGas.Switch_LiquidToGas();  // Liquid→Gas
                    // 이미 Gas면 아무 것도 하지 않음
                }
            }
        }
    }

    bool IsSolidVisible()
    {
        // 고체 상태 판단:
        // - Rigidbody2D가 simulated이고
        // - 콜라이더가 켜져 있고
        // - 렌더러 중 하나라도 enabled인 경우
        bool rbOn  = solidRb  ? solidRb.simulated : true;
        bool colOn = solidCol ? solidCol.enabled  : true;
        bool anyRenderer = (solidRenderers != null) && solidRenderers.Any(r => r && r.enabled);
        return rbOn && colOn && anyRenderer;
    }
}

