using UnityEngine;
namespace Aryx_F22E_StrikeRaptor.FCS
{
    // 4.6.0 bench: the game-side SlatForce is not built here (Plant applies SlatAero itself); FlapSystem only needs the type
    internal class SlatForce : MonoBehaviour { internal FlapSystem flaps; }
}
