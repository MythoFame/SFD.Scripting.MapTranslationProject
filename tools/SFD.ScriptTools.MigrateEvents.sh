#!/bin/sh
set -eu

FILE=${1:-Program.cs}

set -- \
ExplosionHitCallback \
ObjectCreatedCallback \
ObjectDamageCallback \
ObjectTerminatedCallback \
PlayerCreatedCallback \
PlayerDamageCallback \
PlayerDeathCallback \
PlayerKeyInputCallback \
PlayerMeleeActionCallback \
PlayerWeaponAddedActionCallback \
PlayerWeaponRemovedActionCallback \
ProjectileCreatedCallback \
ProjectileHitCallback \
UpdateCallback \
UserJoinCallback \
UserLeaveCallback \
UserMessageCallback

for cb do
  sed -E -i "s/Events\\.${cb}\\.Start/Game.Events.Start${cb}/g" "$FILE"
done
