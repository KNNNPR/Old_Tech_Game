#ifndef OLD_TECH_LIQUID_BLOOD_MOTION_INCLUDED
#define OLD_TECH_LIQUID_BLOOD_MOTION_INCLUDED

float BloodTime()
{
    return _PreviewTime >= 0.0 ? _PreviewTime : _Time.y;
}

float2 BloodFlowDirection()
{
    float2 direction = _FlowDirection.xz;
    return direction / max(length(direction), 0.0001);
}

float2 BloodFlowPosition(float3 positionWS, float time)
{
    return positionWS.xz - BloodFlowDirection() * time * _FlowSpeed;
}

float3 BloodPositionOS(float3 positionOS)
{
    // The bucket material has zero spread. Only the floor puddle's outline moves.
    if (_SpreadStrength <= 0.0) return positionOS;
    float3 positionWS = TransformObjectToWorld(positionOS);
    float2 radial = positionWS.xz - TransformObjectToWorld(float3(0, 0, 0)).xz;
    float distanceFromCenter = length(radial);
    if (distanceFromCenter < 0.0001) return positionOS;
    float2 direction = radial / max(distanceFromCenter, 0.0001);
    float edge = smoothstep(0.15, 1.0, distanceFromCenter / max(_SpreadRadius, 0.001));
    float time = BloodTime();
    float creep = 0.5 - 0.5 * cos(time * _SpreadSpeed);
    float irregularity = 0.8 + 0.2 * sin(atan2(direction.y, direction.x) * 3.0 + time * _SpreadSpeed * 0.35);
    float downstream = 0.7 + 0.3 * saturate(dot(direction, BloodFlowDirection()));
    positionWS.xz += direction * (_SpreadStrength * edge * creep * irregularity * downstream);
    // Leave height unchanged so the blood keeps touching the floor.
    return TransformWorldToObject(positionWS);
}

#endif
