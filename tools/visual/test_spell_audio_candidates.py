import tempfile
import unittest
from pathlib import Path
from spell_audio_candidates import candidates


class CandidateInventory(unittest.TestCase):
    def test_measured_familiar_call_and_comment_stay_distinct(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root/"summon.nss").write_text('void main(){SummonFamiliar();}', encoding="utf-8", newline="")
            (root/"comment.nss").write_text('// SummonFamiliar();', encoding="utf-8", newline="")
            rows = candidates({"318":{"ImpactScript":"summon"},"319":{"ImpactScript":"comment"}},
                              {"36":{"SoundImpact":"SFF_SUMMUND"}}, {}, root)
            self.assertEqual(rows[0]["candidateSounds"][0]["resref"], "sff_summund")
            self.assertEqual(rows[0]["builtinEffects"][0]["function"], "SummonFamiliar")
            self.assertEqual(rows[1]["candidateSounds"], [])

    def test_direct_sources_and_unresolved_paths_stay_distinct(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root/"probe.nss").write_text('''
                // EffectVisualEffect(VFX_UNUSED);
                #include "helper"
                void main() {
                    EffectVisualEffect(VFX_IMPACT);
                    EffectVisualEffect(nDynamic);
                    PlaySound("fixture_sound");
                    EffectAreaOfEffect(AOE_PER_FIXTURE);
                }
            ''', encoding="utf-8", newline="")
            rows = candidates({"7": {"ImpactScript": "PROBE"}, "8": {"ImpactScript": "missing"}},
                {"4": {"SoundImpact": "IMPACT_SOUND", "SoundDuration": "****", "SoundCessastion": "IGNORED", "SoundCessation": "CUSTOM_END"}},
                {"VFX_IMPACT": 4, "VFX_UNUSED": 5}, root)
            self.assertEqual([r["resref"] for r in rows[0]["candidateSounds"]], ["impact_sound", "custom_end", "fixture_sound"])
            self.assertEqual([r["resref"] for r in rows[0]["ignoredSoundFields"]], ["ignored"])
            self.assertEqual(rows[0]["includes"], ["helper"])
            self.assertEqual(rows[0]["areaEffects"], ["AOE_PER_FIXTURE"])
            self.assertEqual(rows[0]["unresolvedVisualArguments"], ["nDynamic"])
            self.assertEqual(rows[1]["status"], "authored-source-not-extracted")


if __name__ == "__main__":
    unittest.main()
