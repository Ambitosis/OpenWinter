import hashlib
import json
import tempfile
import unittest
from types import SimpleNamespace
from pathlib import Path
from unittest.mock import patch
import spell_evidence as evidence

class FixtureProfiles(unittest.TestCase):
    def test_self_target_follows_declared_npc_caster(self):
        script = evidence.make_cast({'row':'174','Range':'L'}, {'target':'self',
            'casterFixture':{'template':'nw_drowmage020','classLevels':[{'class':10,'level':20}]}}, 0)
        self.assertIn('target=caster;', script)
        self.assertIn('GetLevelByClass(10,caster)', script)
        self.assertIn('"spc174"', script)

    def test_weapon_owned_by_declared_caster_and_empty_summon_location(self):
        script = evidence.make_cast({'row':'544','Range':'T'}, {'target':'weapon','caster':'nw_drowmage020'}, 0)
        self.assertIn('GetItemInSlot(INVENTORY_SLOT_RIGHTHAND,caster)', script)
        self.assertNotIn('INVENTORY_SLOT_RIGHTHAND,pc', script)
        script = evidence.make_cast({'row':'174','Range':'L'}, {'target':'location','distance':5}, 0)
        self.assertIn('ActionCastSpellAtLocation(174,GetLocalLocation', script)
        self.assertNotIn('VSpawn', script)
        self.assertNotIn('STANDARD_FACTION_HOSTILE', script)

    def test_hostile_profiles_establish_both_reaction_directions(self):
        script = evidence.make_cast({'row':'58'}, {'target':'ground','extraTargets':[{'x':2,'y':8}]}, 0)
        self.assertIn('SetIsTemporaryEnemy(caster,target);SetIsTemporaryEnemy(target,caster);', script)
        self.assertIn('SetIsTemporaryEnemy(caster,extra);SetIsTemporaryEnemy(extra,caster);', script)
        self.assertIn('"fixture.58.enemy_before."', script)
        script = evidence.make_cast({'row':'1'}, {'target':'ally','extraTargets':[{'x':2,'y':8,'enemy':False}]}, 0)
        self.assertNotIn('SetIsTemporaryEnemy', script)

    def test_action_completion_alone_does_not_prove_execution(self):
        result = evidence.trigger_observations([(1, 'start.107'), (1, 'target.107.1'), (1, 'finished.107')], 107)
        self.assertTrue(result['actionFinished'])
        self.assertEqual(result['triggerStatus'], 'execution-unproven')
        self.assertEqual(result['executionEvidence'], [])

    def test_summon_observation_requires_new_associate(self):
        events = [(0, 'state.174.before.associate.2.0'), (1, 'state.174.spawn.associate.2.1')]
        self.assertEqual(evidence.trigger_observations(events, 174)['executionEvidence'], ['associate-created'])
        events[0] = (0, 'state.174.before.associate.2.1')
        self.assertEqual(evidence.trigger_observations(events, 174)['executionEvidence'], [])

    def test_feat_trigger_uses_real_action_and_rejects_ambiguous_ids(self):
        script = evidence.make_cast({'row':'318','Range':'S'}, {'target':'self','trigger':'feat','feat':303}, 0)
        self.assertIn('ActionUseFeat(303,target)', script)
        self.assertNotIn('ActionCastSpell', script)
        self.assertIn('"spb_feat",303', script)
        for invalid in (None, True, -1, 65536, '303'):
            with self.assertRaises(ValueError):
                evidence.make_cast({'row':'318'}, {'target':'self','trigger':'feat','feat':invalid}, 0)
        with self.assertRaises(ValueError):
            evidence.make_cast({'row':'318'}, {'target':'location','trigger':'feat','feat':303}, 0)

    def test_prepared_cast_does_not_silently_enable_cheat(self):
        script = evidence.make_cast({'row':'102'}, {'target':'self','caster':'pc','cheat':False}, 0)
        self.assertIn('ActionCastSpellAtObject(102,target,METAMAGIC_NONE,FALSE)', script)
        self.assertNotIn('VSpawn', script)
        for invalid in ('false', 0, None):
            with self.assertRaises(ValueError):
                evidence.make_cast({'row':'102'}, {'target':'self','cheat':invalid}, 0)

    def test_changed_stock_character_stops_before_capture_artifact_cleanup(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            output = root/'out/spell-audit'
            evidence.write(root/'config/local.json', {'gameRoot':str(root/'game')})
            character = root/'game/data/lcv/wizard.bic'
            character.parent.mkdir(parents=True)
            character.write_bytes(b'changed synthetic character')
            evidence.write(output/'batches/pcempty102.json', dict(scene='pcempty102',
                fixtureSha256='fixture', character='wizard', characterSha256='old-character'))
            artifact = root/'out/visual/shots/vis_pcempty102/openwinter/prior.png'
            artifact.parent.mkdir(parents=True)
            artifact.write_bytes(b'prior evidence')
            with patch.object(evidence,'ROOT',root), patch.object(evidence,'OUT',output), \
                 patch.object(evidence,'fixture_hash',return_value='fixture'):
                with self.assertRaisesRegex(SystemExit, 'Installed character changed'):
                    evidence.capture(SimpleNamespace(scene='pcempty102',engine='openwinter'))
            self.assertEqual(artifact.read_bytes(), b'prior evidence')

    def test_native_wrong_player_or_slot_cannot_satisfy_requested_context(self):
        rows = [dict(id=107,profile={'expectedBefore':{'wizard_level':15,'slot_spell':107,'name':'Wizard'}})]
        actual = {'107':{'phases':{'before':{'wizard_level':['1'],'slot_spell':['174'],'name':['Aluvian Darkstar']}}}}
        self.assertEqual({x['field'] for x in evidence.context_mismatches(rows,actual)}, {'wizard_level','slot_spell','name'})
        actual['107']['phases']['before'] = {'wizard_level':['15'],'slot_spell':['107'],'name':['Wizard']}
        self.assertEqual(evidence.context_mismatches(rows,actual), [])
        del actual['107']['phases']['before']['name']
        self.assertEqual(evidence.context_mismatches(rows,actual)[0]['field'], 'name')

    def test_ledger_retains_both_contexts_and_independent_trigger_proof(self):
        old_root, old_out = evidence.ROOT, evidence.OUT
        with tempfile.TemporaryDirectory() as directory:
            evidence.ROOT = Path(directory)
            evidence.OUT = evidence.ROOT/'out/spell-audit'
            try:
                evidence.write(evidence.OUT/'stock-spells.tsv', 'row\tLabel\tTargetType\tImpactScript\n318\tSummon_Familiar\t2\tscript\n')
                for index, scene in enumerate(('pilot318', 'broad318')):
                    fixture = 'fixture-' + scene
                    capture = dict(id=318, profile={'target':'self','context':scene}, phases={'spawn':2}, span=8)
                    evidence.write(evidence.OUT/'batches'/f'{scene}.json', dict(scene=scene, fixtureSha256=fixture, rows=[capture]))
                    folder=evidence.ROOT/scene;folder.mkdir()
                    shot=folder/'s318_spawn.png';shot.write_bytes(b'synthetic artifact')
                    evidence.write(evidence.OUT/'captures'/f'{scene}.openwinter.json', dict(fixtureSha256=fixture,
                        folder=str(folder), shots={shot.name:hashlib.sha256(shot.read_bytes()).hexdigest()},
                        revision=scene, complete=True, observations={'318':{'context':scene}}))
                evidence.ledger(None)
                row=evidence.read_optional(evidence.OUT/'presentation-ledger.json')['rows'][0]
                self.assertEqual({x['scene'] for x in row['contexts']}, {'pilot318','broad318'})
                self.assertEqual({x['scene'] for x in row['evidence']}, {'pilot318','broad318'})
                self.assertEqual({x['observations']['context'] for x in row['triggerEvidence']}, {'pilot318','broad318'})
                self.assertEqual(row['axes']['targetFx'], 'captured-unreviewed')
                self.assertEqual(row['axes']['audio'], 'unverified')
                self.assertNotIn('verified', row['axes'].values())
            finally:
                evidence.ROOT, evidence.OUT = old_root, old_out

    def test_capture_rejects_unsafe_context_names_before_reading_artifacts(self):
        for scene in ('../spb000', 'assoc317/../../', 'UPPER317', 'toolongprefix317', 'assoc'):
            with self.assertRaises(SystemExit):
                evidence.capture(SimpleNamespace(scene=scene, engine='openwinter'))

    def test_generation_preserves_authored_and_changed_legacy_scenes(self):
        old_root, old_out = evidence.ROOT, evidence.OUT
        with tempfile.TemporaryDirectory() as directory:
            evidence.ROOT = Path(directory)
            evidence.OUT = evidence.ROOT/'out/spell-audit'
            try:
                folder = evidence.ROOT/'parity/visual/scenes/assoc317'
                folder.mkdir(parents=True)
                script = folder/'vis_main.nss';script.write_text('authored work')
                with self.assertRaises(FileExistsError): evidence.generated_directory('assoc317')
                self.assertEqual(script.read_text(), 'authored work')
                evidence.write(evidence.OUT/'batches/assoc317.json', dict(scene='assoc317', fixtureSha256=evidence.fixture_hash('assoc317')))
                script.write_text('changed after generation')
                with self.assertRaises(FileExistsError): evidence.generated_directory('assoc317')
                evidence.write(evidence.OUT/'batches/assoc317.json', dict(scene='assoc317', fixtureSha256=evidence.fixture_hash('assoc317')))
                before = evidence.fixture_hash('assoc317')
                self.assertEqual(evidence.generated_directory('assoc317'), folder)
                self.assertEqual(evidence.fixture_hash('assoc317'), before)
                self.assertTrue((folder/'.spell-evidence-owned').exists())
                self.assertEqual((folder/'.gitignore').read_text(), '*\n')
                # A marked generated directory remains ours even without its old manifest.
                (evidence.OUT/'batches/assoc317.json').unlink()
                self.assertEqual(evidence.generated_directory('assoc317'), folder)
            finally:
                evidence.ROOT, evidence.OUT = old_root, old_out

    def test_runtime_asset_mutation_invalidates_fixture_hash(self):
        old = evidence.ROOT
        with tempfile.TemporaryDirectory() as directory:
            evidence.ROOT = Path(directory)
            folder = evidence.ROOT/'parity/visual/scenes/spb001'
            folder.mkdir(parents=True)
            try:
                (folder/'scene.json').write_text('{}')
                initial = evidence.fixture_hash('spb001')
                (folder/'own.mdl').write_text('authored control')
                model = evidence.fixture_hash('spb001')
                self.assertNotEqual(initial, model)
                (folder/'own.tga').write_bytes(b'authored texture')
                self.assertNotEqual(model, evidence.fixture_hash('spb001'))
            finally:
                evidence.ROOT = old

if __name__ == '__main__': unittest.main()
