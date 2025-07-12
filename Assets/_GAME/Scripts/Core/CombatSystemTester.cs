using UnityEngine;
using _GAME.Scripts.Combat;
using _GAME.Scripts.Core;

/// <summary>
/// Quick testing script for combat system validation
/// Attach to any GameObject in scene to run tests
/// </summary>
public class CombatSystemTester : MonoBehaviour
{
    [Header("Test Configuration")] [SerializeField] private bool  enableAutoTest = false;
    [SerializeField]                                private float testInterval   = 2f;

    [Header("References")] [SerializeField] private PlayerController player1;
    [SerializeField]                        private PlayerController player2;

    private float nextTestTime;

    private void Start()
    {
        if (player1 == null) player1 = FindObjectOfType<PlayerController>();

        ValidateSetup();
    }

    private void Update()
    {
        if (enableAutoTest && Time.time > nextTestTime)
        {
            nextTestTime = Time.time + testInterval;
            RunAutomatedTest();
        }

        // Manual test hotkeys
        if (Input.GetKeyDown(KeyCode.F1)) TestBasicSetup();
        if (Input.GetKeyDown(KeyCode.F2)) TestCombatFlow();
        if (Input.GetKeyDown(KeyCode.F3)) TestNetworkSync();
        if (Input.GetKeyDown(KeyCode.F4)) TestEdgeCases();
    }

    /// <summary>
    /// Validate basic system setup
    /// </summary>
    [ContextMenu("Test Basic Setup")]
    public void TestBasicSetup()
    {
        Debug.Log("=== COMBAT SYSTEM SETUP TEST ===");

        bool allPassed = true;

        // Test 1: Check PlayerController
        if (player1 == null)
        {
            Debug.LogError("❌ No PlayerController found in scene");
            allPassed = false;
        }
        else
        {
            Debug.Log("✅ PlayerController found");
        }

        // Test 2: Check ComboController
        var comboController = player1?.GetComponent<ComboController>();
        if (comboController == null)
        {
            Debug.LogError("❌ ComboController component missing on player");
            allPassed = false;
        }
        else
        {
            Debug.Log("✅ ComboController component found");

            // Test 3: Check Combo Definition
            var comboDef = comboController.GetComboDefinition();
            if (comboDef == null)
            {
                Debug.LogError("❌ ComboDefinitionSO not assigned");
                allPassed = false;
            }
            else if (!comboDef.IsValidCombo())
            {
                Debug.LogError("❌ ComboDefinitionSO is invalid");
                allPassed = false;
            }
            else
            {
                Debug.Log($"✅ ComboDefinitionSO valid: {comboDef.ComboName} ({comboDef.ComboLength} attacks)");
            }
        }

        // Test 4: Check InputManager
        var inputManager = FindObjectOfType<InputManager>();
        if (inputManager == null)
        {
            Debug.LogError("❌ InputManager not found in scene");
            allPassed = false;
        }
        else
        {
            Debug.Log("✅ InputManager found");
        }

        // Test 5: Check Network Components
        var networkObject = player1?.GetComponent<Fusion.NetworkObject>();
        if (networkObject == null)
        {
            Debug.LogError("❌ NetworkObject missing on player");
            allPassed = false;
        }
        else
        {
            Debug.Log("✅ NetworkObject found");
        }

        // Final result
        if (allPassed)
        {
            Debug.Log("🎉 ALL SETUP TESTS PASSED!");
        }
        else
        {
            Debug.LogError("❌ SETUP TESTS FAILED - Check errors above");
        }
    }

    /// <summary>
    /// Test combat flow and combo system
    /// </summary>
    [ContextMenu("Test Combat Flow")]
    public void TestCombatFlow()
    {
        Debug.Log("=== COMBAT FLOW TEST ===");

        if (player1 == null)
        {
            Debug.LogError("❌ No player to test");
            return;
        }

        var comboController = player1.GetComponent<ComboController>();
        if (comboController == null)
        {
            Debug.LogError("❌ No ComboController to test");
            return;
        }

        // Test attack input validation
        Debug.Log("Testing attack input validation...");

        // Simulate neutral attack
        bool canAttack = comboController.CanPerformAttack(_GAME.Scripts.Combat.AttackInputType.Neutral);
        Debug.Log($"Can perform neutral attack: {canAttack}");

        // Test combo state
        Debug.Log($"Current combo index: {comboController.CurrentComboIndex}");
        Debug.Log($"Is in combo: {comboController.IsInCombo}");
        Debug.Log($"Attack phase: {comboController.AttackPhase}");

        // Log combo definition details
        var comboDef = comboController.GetComboDefinition();
        if (comboDef != null)
        {
            Debug.Log($"Combo info: {comboDef.GetComboInfo()}");
        }
    }

    /// <summary>
    /// Test network synchronization
    /// </summary>
    [ContextMenu("Test Network Sync")]
    public void TestNetworkSync()
    {
        Debug.Log("=== NETWORK SYNC TEST ===");

        var players = FindObjectsOfType<PlayerController>();
        Debug.Log($"Found {players.Length} players in scene");

        foreach (var player in players)
        {
            var networkObj = player.GetComponent<Fusion.NetworkObject>();
            if (networkObj != null)
            {
                Debug.Log($"Player {player.name}: " + $"HasInputAuthority: {networkObj.HasInputAuthority}, " + $"HasStateAuthority: {networkObj.HasStateAuthority}, " + $"InputAuthority: {networkObj.InputAuthority}");
            }

            var comboController = player.GetComponent<ComboController>();
            if (comboController != null)
            {
                Debug.Log($"  Combo Index: {comboController.CurrentComboIndex}, " + $"Phase: {comboController.AttackPhase}, " + $"Energy: {comboController.CurrentEnergy}");
            }
        }
    }

    /// <summary>
    /// Test edge cases and error handling
    /// </summary>
    [ContextMenu("Test Edge Cases")]
    public void TestEdgeCases()
    {
        Debug.Log("=== EDGE CASES TEST ===");

        if (player1 == null) return;

        var comboController = player1.GetComponent<ComboController>();
        if (comboController == null) return;

        // Test invalid attack input
        Debug.Log("Testing invalid attack inputs...");
        bool canAttackUp      = comboController.CanPerformAttack(_GAME.Scripts.Combat.AttackInputType.Up);
        bool canAttackForward = comboController.CanPerformAttack(_GAME.Scripts.Combat.AttackInputType.Forward);

        Debug.Log($"Can attack Up: {canAttackUp}");
        Debug.Log($"Can attack Forward: {canAttackForward}");

        // Test combo reset
        Debug.Log("Testing combo reset...");
        comboController.ResetCombo();
        Debug.Log($"After reset - Combo Index: {comboController.CurrentComboIndex}, Phase: {comboController.AttackPhase}");
    }

    /// <summary>
    /// Automated test sequence
    /// </summary>
    private void RunAutomatedTest()
    {
        Debug.Log("🤖 Running automated test sequence...");
        // Cycle through different tests
        var testType = (int)(Time.time / testInterval) % 4;

        switch (testType)
        {
            case 0: TestBasicSetup(); break;
            case 1: TestCombatFlow(); break;
            case 2: TestNetworkSync(); break;
            case 3: TestEdgeCases(); break;
        }
    }

    /// <summary>
    /// Validate setup when script starts
    /// </summary>
    private void ValidateSetup()
    {
        Debug.Log("🔍 Validating combat system setup...");

        // Auto-find player if not assigned
        if (player1 == null)
        {
            player1 = FindObjectOfType<PlayerController>();
            if (player1 != null)
            {
                Debug.Log($"Auto-found player: {player1.name}");
            }
        }

        // Show test instructions
        Debug.Log("💡 COMBAT SYSTEM TESTER READY");
        Debug.Log("Press F1: Test Basic Setup");
        Debug.Log("Press F2: Test Combat Flow");
        Debug.Log("Press F3: Test Network Sync");
        Debug.Log("Press F4: Test Edge Cases");
        Debug.Log("Or enable Auto Test in inspector");
    }
}