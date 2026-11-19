"""Validate local guardian progress without letting legacy snapshots downgrade it.

The desktop save remains the authority for combat. Receipts bind local transitions
and retries; they are not a claim of server-side anti-cheat or battle simulation.
"""
import re

from backend.store import Problem


FIELDS = ('guardianLandVersion', 'guardianLandState', 'guardianEncounterId',
          'guardianOccupationId', 'guardianLastEncounterId', 'guardianLastOutcome',
          'guardianLegacyOwnership', 'guardianReplay')


def _id(value):
    return isinstance(value, str) and re.fullmatch(r'[a-f0-9]{32}', value) is not None


def can_create(state):
    return (state.get('isVisitor') is False and state.get('expanded') is True
            and (not state.get('guardianLandVersion') or (state.get('guardianLandVersion') == 1 and state.get('guardianLandState') == 'occupied')))


def snapshot_fields(raw, old, profile):
    if raw['isVisitor']:
        return {}
    version = raw.get('guardianLandVersion', 0)
    if type(version) is not int or version not in (0, 1):
        raise Problem('守卫土地存档版本无效。')
    if not version:
        if profile.get('guardianLandVersion') == 1:
            raise Problem('新版土地状态不能退回旧协议，请重新读取本地存档。', 409)
        return {}
    if raw['plotId'] != 'home' or not all(k in raw for k in FIELDS):
        raise Problem('守卫土地字段不完整或不属于自己的家园。')
    s = {k: raw[k] for k in FIELDS}
    if any(type(s[k]) is not bool for k in ('guardianLegacyOwnership', 'guardianReplay')):
        raise Problem('守卫土地布尔状态无效。')
    if any(not isinstance(s[k], str) for k in FIELDS if k not in ('guardianLandVersion', 'guardianLegacyOwnership', 'guardianReplay')):
        raise Problem('守卫土地状态格式无效。')
    state = s['guardianLandState']; active = state == 'challenging'; occupied = raw['expanded']
    encounter = s['guardianEncounterId']; occupation = s['guardianOccupationId']
    last = s['guardianLastEncounterId']; outcome = s['guardianLastOutcome']; legacy = s['guardianLegacyOwnership']
    if (state not in {'guarded', 'challenging', 'occupied'}
            or (state == 'guarded' and occupied) or (state == 'occupied' and not occupied)
            or (occupied != _id(occupation)) or (not occupied and occupation)
            or (active != _id(encounter)) or (not active and encounter)
            or s['guardianReplay'] != (active and occupied)
            or (legacy and not occupied)
            or (active and not occupied and raw['permits'] < 1)
            or outcome not in {'', 'victory', 'defeat', 'interrupted'}
            or (occupied and not legacy and not outcome)
            or (occupied and not legacy and occupation == last and outcome != 'victory')
            or bool(outcome) != _id(last) or (not outcome and last)):
        raise Problem('守卫土地、挑战结果与许可状态不一致。', 409)
    if old.get('expanded') and not occupied:
        raise Problem('已经开放的土地不能退回守卫占领状态。', 409)
    if old.get('guardianLandVersion') == 1:
        if legacy != old.get('guardianLegacyOwnership'):
            raise Problem('新版土地不能伪装为旧档已占领土地。', 409)
        if old.get('expanded') and occupation != old.get('guardianOccupationId'):
            raise Problem('已占领土地的凭据不能改变。', 409)
        if old.get('guardianLandState') == 'challenging':
            # A snapshot may skip retreats/retries while offline. Combat validates
            # each encounter locally; only its total one-time debit is observable here.
            expected_permits = old['permits'] - (1 if occupied and not old.get('expanded') else 0)
            if raw['permits'] != expected_permits:
                raise Problem('挑战结果只能在首次获胜时消耗一张许可。', 409)
        if occupied and not old.get('expanded') and (occupation == old.get('guardianLastEncounterId') and old.get('guardianLastOutcome') in {'defeat', 'interrupted'}):
            raise Problem('失败或中断的挑战不能再次结算为占领。', 409)
    # New acquisitions need the matching victory receipt, including reconnects
    # that happened entirely offline between the two snapshots.
    if occupied and not legacy and not old.get('expanded'):
        # The latest result can be a later replay, while occupation keeps the
        # original winning encounter receipt permanently.
        if not outcome or (occupation == last and outcome != 'victory'):
            raise Problem('击败守卫并保存占领结果后，才能开放新土地。', 409)
    return s
