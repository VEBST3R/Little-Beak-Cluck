using UnityEngine;

namespace LittleBeakCluck.Enemies
{
    /// <summary>
    /// Phantom-like flying enemy: patrols zone → parabolic dive attack → return to patrol.
    /// </summary>
    public class FlyingEnemyBehaviour : EnemyBehaviour
    {
        private enum FlightState
        {
            Patrolling,
            PreparingDive,
            Diving,
            Climbing,
            Returning
        }

        private FlyingEnemyConfig _config;
        private FlightState _state;
        private float _stateStartTime;

        // Patrol
        private Vector2 _spawnPosition;
        private Vector2 _patrolTarget;
        private float _patrolWaitTimer;
        private Vector2 _patrolVelocity;

        // Dive
        private Vector2 _diveStartPosition;
        private Vector2 _diveControlPoint;
        private Vector2 _diveEndPosition;
        private float _diveProgress;
        private bool _attackTriggered;
        private float _attackTriggerTime;
        private bool _hasDealtDamage;
        private float _attackHorizontalSign = 1f;
        private Vector2 _attackPlayerAnchor;

        // Climb
        private Vector2 _climbStartPosition;
        private Vector2 _climbControlPoint;
        private Vector2 _climbEndPosition;
        private float _climbProgress;

        // Predictive targeting
        private bool _playerTrackingInitialized;
        private Vector2 _playerPreviousPosition;
        private Vector2 _playerVelocity;
        private bool _anchorInitialized;
        private Vector2 _predictedAnchor;
        private Vector2 _predictedAnchorVelocity;

        // Facing smoothing
        private float _smoothedYaw;
        private float _smoothedRoll;
        private float _rollVelocity;
        private bool _isFacingRight = true;
        private float _facingFlipTimer;

        private const float MinTimeStep = 0.0001f;
        private const float GizmoAlpha = 0.8f;

        private bool HasActivePlayer => HasPlayer && Player != null;
        private Vector2 CurrentPosition => Rigidbody.position;
        private float SafeDeltaTime => Mathf.Max(Time.deltaTime, MinTimeStep);
        private float SafeFixedDeltaTime => Mathf.Max(Time.fixedDeltaTime, MinTimeStep);
        private float AttackHorizontalDistance => Mathf.Abs(_config.attackHorizontalDistance);
        private float AttackEntryHeight => _config.attackEntryHeight;
        private float AttackExitHeight => _config.attackExitHeight;
        private float PredictionSmoothingTime => Mathf.Max(MinTimeStep, _config.predictionSmoothingTime);
        private float PatrolSmoothingTime => Mathf.Max(MinTimeStep, _config.patrolSmoothingTime);

        protected override void Awake()
        {
            base.Awake();

            _config = Config as FlyingEnemyConfig;
            if (_config == null)
            {
                Debug.LogError($"[{name}] requires a {nameof(FlyingEnemyConfig)} assigned to the Enemy Config field.", this);
                enabled = false;
                return;
            }

            Rigidbody.gravityScale = 0f;
            Rigidbody.linearDamping = 0f;
            Rigidbody.angularDamping = 0f;
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            if (!enabled)
                return;

            _spawnPosition = CurrentPosition;
            Rigidbody.gravityScale = 0f;
            Rigidbody.linearVelocity = Vector2.zero;
            ChangeState(FlightState.Patrolling);
            PickNewPatrolTarget();
            _attackHorizontalSign = 1f;
            _patrolVelocity = Vector2.zero;
            _playerTrackingInitialized = false;
            _playerVelocity = Vector2.zero;
            _playerPreviousPosition = CurrentPosition;
            _anchorInitialized = false;
            _predictedAnchor = CurrentPosition;
            _predictedAnchorVelocity = Vector2.zero;
            _smoothedYaw = transform.localEulerAngles.y;
            _smoothedRoll = transform.localEulerAngles.z;
            _rollVelocity = 0f;
            _isFacingRight = _smoothedYaw < 90f || _smoothedYaw > 270f;
            _facingFlipTimer = 0f;
        }

        protected override void Update()
        {
            TryPlayRevealSound();

            if (!IsAlive || _config == null)
                return;

            if (!HasPlayer && AutoFindPlayer)
            {
                if (!TryAssignPlayer())
                    return;
            }

            UpdatePlayerTracking();
            UpdatePredictedAnchor();

            switch (_state)
            {
                case FlightState.Patrolling:
                    UpdatePatrol();
                    break;
                case FlightState.PreparingDive:
                    UpdatePreparingDive();
                    break;
                case FlightState.Diving:
                    UpdateDiving();
                    break;
                case FlightState.Climbing:
                    UpdateClimbing();
                    break;
                case FlightState.Returning:
                    UpdateReturning();
                    break;
            }
        }

        protected override void FixedUpdate()
        {
            if (!IsAlive || _config == null || IsKnockedBack)
                return;

            // Diving & climbing use bezier movement handled in Update
            if (_state != FlightState.Diving && _state != FlightState.Climbing)
            {
                float speed = GetCurrentSpeed();
                Vector2 current = CurrentPosition;
                Vector2 next = Vector2.SmoothDamp(current, _patrolTarget, ref _patrolVelocity, PatrolSmoothingTime, speed, SafeFixedDeltaTime);
                Rigidbody.MovePosition(next);
            }
        }

        private void UpdatePatrol()
        {
            // Перевіряємо чи можна атакувати
            if (HasPlayer && CanAttack())
            {
                BeginDive();
                return;
            }

            // Перевіряємо чи ми в межах статичної патрульної зони
            Vector2 zoneCenter = GetPatrolZoneCenter();
            Vector2 position = CurrentPosition;
            Vector2 toZoneCenter = zoneCenter - position;
            float distToZoneCenterSqr = toZoneCenter.sqrMagnitude;
            Vector2 halfSize = _config.patrolZoneSize * 0.5f;
            float maxDistFromCenter = Mathf.Max(halfSize.x, halfSize.y);
            float zoneOvershoot = maxDistFromCenter * 1.5f;
            float zoneOvershootSqr = zoneOvershoot * zoneOvershoot;

            // Якщо занадто далеко від зони - летимо прямо до центру зони
            if (distToZoneCenterSqr > zoneOvershootSqr)
            {
                _patrolTarget = zoneCenter;
                _patrolVelocity = Vector2.zero;
                UpdateFacing(toZoneCenter.normalized);

#if UNITY_EDITOR
                if (Time.frameCount % 60 == 0) // Лог кожну секунду
                {
                    float distToZoneCenter = Mathf.Sqrt(distToZoneCenterSqr);
                    Debug.Log($"[{name}] Летить до центру патруля (відстань: {distToZoneCenter:F1}м)");
                }
#endif
                return;
            }

            // В зоні - літаємо між точками патрулювання
            Vector2 toTarget = _patrolTarget - position;

            // Досягли точки патрулювання
            if (toTarget.sqrMagnitude < 0.25f)
            {
                _patrolWaitTimer -= Time.deltaTime;

                if (_patrolWaitTimer <= 0f)
                {
                    PickNewPatrolTarget();
                    _patrolWaitTimer = _config.patrolWaitTime;
                }
            }
            else
            {
                // Орієнтація до руху
                UpdateFacing(toTarget.normalized);
            }
        }

        private void UpdatePreparingDive()
        {
            // Перевірка чи гравець не втік занадто далеко
            if (!HasPlayer)
            {
                AbortDive();
                return;
            }

            // Таймаут - якщо підготовка триває занадто довго
            if (Time.time - _stateStartTime > 5f)
            {
                AbortDive();
                return;
            }

            Vector2 desiredAnchor = GetCurrentAttackAnchor();
            Vector2 previousAnchor = _attackPlayerAnchor;
            Vector2 anchorDelta = desiredAnchor - previousAnchor;
            float anchorDeltaSqr = anchorDelta.sqrMagnitude;

            if (anchorDeltaSqr > 0.0001f)
            {
                _attackPlayerAnchor = desiredAnchor;

                _diveStartPosition = desiredAnchor + BuildEntryOffset(_attackHorizontalSign);
                _patrolTarget = _diveStartPosition;

                float largeShiftThreshold = Mathf.Max(0.1f, AttackHorizontalDistance * 0.25f);
                if (anchorDeltaSqr >= largeShiftThreshold * largeShiftThreshold)
                {
                    _patrolVelocity = Vector2.zero;
                }
            }

            Vector2 toTarget = _diveStartPosition - CurrentPosition;

            if (toTarget.sqrMagnitude < 0.1f)
            {
                // Reached dive start position, begin dive
                StartDive();
            }
            else
            {
                UpdateFacing(toTarget.normalized);
            }
        }

        private void UpdateDiving()
        {
            if (!HasPlayer)
            {
                AbortDive();
                return;
            }

            Vector2 desiredAnchor = GetCurrentAttackAnchor();
            if ((_diveEndPosition - desiredAnchor).sqrMagnitude > 0.0001f)
            {
                _attackPlayerAnchor = desiredAnchor;
                _diveEndPosition = desiredAnchor;

                Vector2 midpoint = (_diveStartPosition + _diveEndPosition) * 0.5f;
                float depth = Mathf.Abs(_config.diveArcDepth);
                _diveControlPoint = midpoint + Vector2.down * depth;
            }

            float prevProgress = _diveProgress;
            float diveDuration = Mathf.Max(0.01f, _config.diveDuration);
            _diveProgress += Time.deltaTime / diveDuration;
            _diveProgress = Mathf.Min(_diveProgress, 1f);

            Vector2 previous = CurrentPosition;
            Vector2 current = EvaluateQuadraticBezier(_diveStartPosition, _diveControlPoint, _diveEndPosition, _diveProgress);
            Rigidbody.position = current;

            Vector2 velocity = (current - previous) / SafeDeltaTime;
            Rigidbody.linearVelocity = velocity;
            if (velocity.sqrMagnitude > 0.0001f)
            {
                UpdateFacing(velocity.normalized);
            }

            if (_attackTriggered && !_hasDealtDamage &&
                Time.time - _attackTriggerTime >= Mathf.Max(0f, _config.attackImpactDelay) &&
                CheckPlayerInRange())
            {
                OnDiveAttackHit();
            }

            if (_diveProgress >= 1f)
            {
                StartClimb();
                return;
            }

            // Safety: if we barely moved (curve collapsed) advance to climb
            if (Mathf.Approximately(prevProgress, _diveProgress))
            {
                StartClimb();
            }
        }

        private void UpdateReturning()
        {
            if (!HasPlayer)
            {
                ChangeState(FlightState.Patrolling);
                PickNewPatrolTarget();
                return;
            }

            // Оновлюємо цільову точку щоб летіти до статичної патрульної зони
            Vector2 zoneCenter = GetPatrolZoneCenter();

            Vector2 position = CurrentPosition;
            Vector2 toTarget = _patrolTarget - position;
            float distToZoneCenterSqr = (position - zoneCenter).sqrMagnitude;
            Vector2 halfSize = _config.patrolZoneSize * 0.5f;
            float maxDistFromCenter = Mathf.Max(halfSize.x, halfSize.y);

            // Якщо досягли зони або вже досить близько
            if (toTarget.sqrMagnitude < 0.5f || distToZoneCenterSqr < maxDistFromCenter * maxDistFromCenter)
            {
                // Back in patrol zone
                ChangeState(FlightState.Patrolling);
                PickNewPatrolTarget();
            }
            else
            {
                UpdateFacing(toTarget.normalized);
            }
        }

        private bool CanAttack()
        {
            if (!HasPlayer)
                return false;

            return Time.time >= LastAttackTime + _config.postAttackDelay;
        }

        private void BeginDive()
        {
            if (!HasPlayer)
                return;

            // Calculate dive start position: above and to the side of player (with optional anchor offset)
            Vector2 attackAnchor = GetCurrentAttackAnchor();
            _attackPlayerAnchor = attackAnchor;

            float horizontalDir = Mathf.Sign(CurrentPosition.x - attackAnchor.x);
            if (Mathf.Approximately(horizontalDir, 0f))
            {
                horizontalDir = Random.value < 0.5f ? -1f : 1f;
            }

            _attackHorizontalSign = horizontalDir;

            _diveStartPosition = attackAnchor + BuildEntryOffset(horizontalDir);

            _patrolTarget = _diveStartPosition;
            _patrolVelocity = Vector2.zero;
            ChangeState(FlightState.PreparingDive);
        }

        private void StartDive()
        {
            if (!HasPlayer)
            {
                AbortDive();
                return;
            }

            Vector2 attackAnchor = GetCurrentAttackAnchor();
            _attackPlayerAnchor = attackAnchor;
            _diveEndPosition = attackAnchor;

            Vector2 midpoint = (_diveStartPosition + _diveEndPosition) * 0.5f;
            float depth = Mathf.Abs(_config.diveArcDepth);
            _diveControlPoint = midpoint + Vector2.down * depth;

            _diveProgress = 0f;
            _attackTriggered = true;
            _attackTriggerTime = Time.time;
            _hasDealtDamage = false;
            Animator.SetTrigger(AttackHash);
            ChangeState(FlightState.Diving);
        }

        private void CompleteDive()
        {
            if (_state == FlightState.Diving)
            {
                StartClimb();
            }
            else
            {
                LastAttackTime = Time.time;
                ReturnToPatrolZone();
            }
        }

        private void AbortDive()
        {
            // Встановлюємо час атаки, щоб не спамити спробами
            LastAttackTime = Time.time;

#if UNITY_EDITOR
            Debug.Log($"[{name}] Атака скасована - гравець відійшов або таймаут");
#endif

            _attackHorizontalSign = 1f;
            ReturnToPatrolZone();
        }

        private void ReturnToPatrolZone()
        {
            // Pick a safe point in patrol zone to return to
            _patrolTarget = GetRandomPatrolPoint();
            _patrolVelocity = Vector2.zero;
            ChangeState(FlightState.Returning);
        }

        private void PickNewPatrolTarget()
        {
            _patrolTarget = GetRandomPatrolPoint();
            _patrolVelocity = Vector2.zero;
        }

        private Vector2 GetRandomPatrolPoint()
        {
            Vector2 zoneCenter = GetPatrolZoneCenter();

            Vector2 halfSize = _config.patrolZoneSize * 0.5f;

            return new Vector2(
                zoneCenter.x + Random.Range(-halfSize.x, halfSize.x),
                zoneCenter.y + Random.Range(-halfSize.y, halfSize.y)
            );
        }

        private float GetCurrentSpeed()
        {
            return _state switch
            {
                FlightState.Patrolling => _config.patrolMoveSpeed,
                FlightState.PreparingDive => _config.patrolMoveSpeed * 1.5f,
                FlightState.Climbing => _config.returnSpeed,
                FlightState.Returning => _config.returnSpeed,
                _ => _config.patrolMoveSpeed
            };
        }

        private Vector2 GetLateralDirection(float horizontalSign)
        {
            if (Mathf.Abs(horizontalSign) < 0.01f)
            {
                return Vector2.right;
            }

            return new Vector2(Mathf.Sign(horizontalSign), 0f);
        }

        private Vector2 BuildEntryOffset(float horizontalSign)
        {
            return GetLateralDirection(horizontalSign) * AttackHorizontalDistance + Vector2.up * AttackEntryHeight;
        }

        private Vector2 BuildExitOffset(float horizontalSign)
        {
            return GetLateralDirection(horizontalSign) * AttackHorizontalDistance + Vector2.up * AttackExitHeight;
        }

        private Vector2 GetPlayerPosition()
        {
            return HasActivePlayer ? (Vector2)Player.position : CurrentPosition;
        }

        private void ChangeState(FlightState newState)
        {
            _state = newState;
            _stateStartTime = Time.time;

            switch (newState)
            {
                case FlightState.Diving:
                case FlightState.Climbing:
                    Rigidbody.gravityScale = 0f; // We handle gravity manually
                    break;
                default:
                    Rigidbody.gravityScale = 0f;
                    Rigidbody.linearVelocity = Vector2.zero;
                    break;
            }
        }

        private void UpdateClimbing()
        {
            float prevProgress = _climbProgress;
            float climbDuration = Mathf.Max(0.01f, _config.climbDuration);
            _climbProgress += Time.deltaTime / climbDuration;
            _climbProgress = Mathf.Min(_climbProgress, 1f);

            Vector2 previous = CurrentPosition;
            Vector2 current = EvaluateQuadraticBezier(_climbStartPosition, _climbControlPoint, _climbEndPosition, _climbProgress);
            Rigidbody.position = current;

            Vector2 velocity = (current - previous) / SafeDeltaTime;
            Rigidbody.linearVelocity = velocity;
            if (velocity.sqrMagnitude > 0.0001f)
            {
                UpdateFacing(velocity.normalized);
            }
            if (_climbProgress >= 1f || Mathf.Approximately(prevProgress, _climbProgress))
            {
                LastAttackTime = Time.time;
                ChangeState(FlightState.Patrolling);
                PickNewPatrolTarget();
                _patrolWaitTimer = _config.patrolWaitTime;
                _attackHorizontalSign = 1f;
            }
        }

        private void StartClimb()
        {
            _climbStartPosition = CurrentPosition;

            if (HasPlayer)
            {
                _attackPlayerAnchor = GetCurrentAttackAnchor();
            }

            _climbEndPosition = _attackPlayerAnchor + BuildExitOffset(-_attackHorizontalSign);

            Vector2 midpoint = (_climbStartPosition + _climbEndPosition) * 0.5f;
            _climbControlPoint = midpoint + Vector2.up * Mathf.Abs(_config.climbArcHeight);

            _climbProgress = 0f;
            ChangeState(FlightState.Climbing);
        }

        private void UpdatePlayerTracking()
        {
            if (!Application.isPlaying || _config == null)
                return;

            if (!HasActivePlayer)
            {
                _playerTrackingInitialized = false;
                _playerVelocity = Vector2.zero;
                return;
            }

            Vector2 currentPosition = GetPlayerPosition();

            if (!_playerTrackingInitialized)
            {
                _playerTrackingInitialized = true;
                _playerPreviousPosition = currentPosition;
                _playerVelocity = Vector2.zero;
                return;
            }

            float deltaTime = SafeDeltaTime;
            Vector2 instantaneousVelocity = (currentPosition - _playerPreviousPosition) / deltaTime;

            float responsiveness = Mathf.Max(0f, _config.predictionVelocityResponsiveness);
            float blend = responsiveness <= 0f ? 1f : 1f - Mathf.Exp(-responsiveness * deltaTime);
            blend = Mathf.Clamp01(blend);

            _playerVelocity = Vector2.Lerp(_playerVelocity, instantaneousVelocity, blend);
            _playerPreviousPosition = currentPosition;
        }

        private void UpdatePredictedAnchor()
        {
            if (!Application.isPlaying || _config == null)
                return;

            Vector2 basePosition = HasActivePlayer ? GetPlayerPosition() : CurrentPosition;

            if (!_anchorInitialized)
            {
                _anchorInitialized = true;
                _predictedAnchor = basePosition;
                _predictedAnchorVelocity = Vector2.zero;
            }

            Vector2 targetBase = basePosition;

            if (!_config.enablePredictiveTargeting)
            {
                _predictedAnchor = basePosition;
                _predictedAnchorVelocity = Vector2.zero;
                return;
            }

            if (HasActivePlayer)
            {
                float estimatedTime = EstimateTimeToImpact();
                if (!float.IsFinite(estimatedTime))
                {
                    estimatedTime = _config.predictionDefaultLeadTime;
                }

                float maxLeadTime = Mathf.Max(0f, _config.predictionMaxLeadTime);
                float clamped = Mathf.Clamp(estimatedTime, 0f, maxLeadTime);

                if (clamped <= 0f)
                {
                    clamped = Mathf.Max(0f, _config.predictionDefaultLeadTime);
                }

                Vector2 offset = _playerVelocity * clamped;
                float maxOffset = Mathf.Max(0f, _config.predictionMaxOffset);
                float maxOffsetSqr = maxOffset * maxOffset;
                if (maxOffset > 0f && offset.sqrMagnitude > maxOffsetSqr)
                {
                    offset = offset.normalized * maxOffset;
                }

                targetBase += offset;

                float magnetRange = Mathf.Max(0f, _config.predictionMagnetRange);
                if (magnetRange > 0f)
                {
                    Vector2 toBase = targetBase - basePosition;
                    float anchorDistanceSqr = toBase.sqrMagnitude;
                    float magnetRangeSqr = magnetRange * magnetRange;
                    if (anchorDistanceSqr < magnetRangeSqr)
                    {
                        float anchorDistance = Mathf.Sqrt(anchorDistanceSqr);
                        float magnetBlend = Mathf.InverseLerp(magnetRange, 0f, anchorDistance);
                        targetBase = Vector2.Lerp(targetBase, basePosition, magnetBlend);
                    }
                }

                if (_state == FlightState.PreparingDive)
                {
                    float catchupDistance = Mathf.Max(0.5f, AttackHorizontalDistance);
                    float distanceToStart = Vector2.Distance(CurrentPosition, _diveStartPosition);
                    float followFactor = Mathf.InverseLerp(catchupDistance, 0f, distanceToStart);
                    targetBase = Vector2.Lerp(_predictedAnchor, targetBase, followFactor);
                }
            }

            _predictedAnchor = Vector2.SmoothDamp(_predictedAnchor, targetBase, ref _predictedAnchorVelocity, PredictionSmoothingTime, Mathf.Infinity, Time.deltaTime);
        }

        private float EstimateTimeToImpact()
        {
            switch (_state)
            {
                case FlightState.PreparingDive:
                    {
                        float distanceToStart = Vector2.Distance(CurrentPosition, _diveStartPosition);
                        float approachSpeed = Mathf.Max(0.1f, GetCurrentSpeed());
                        float timeToStart = distanceToStart / approachSpeed;
                        return timeToStart + Mathf.Max(0f, _config.diveDuration);
                    }
                case FlightState.Diving:
                    return Mathf.Max(0f, (1f - _diveProgress) * Mathf.Max(0.01f, _config.diveDuration));
                case FlightState.Patrolling:
                case FlightState.Returning:
                    return Mathf.Max(0f, _config.predictionDefaultLeadTime);
                case FlightState.Climbing:
                default:
                    return 0f;
            }
        }

        private Vector2 GetPatrolZoneCenter()
        {
            return _spawnPosition + _config.patrolZoneOffset;
        }

        private Vector2 GetCurrentAttackAnchor()
        {
            if (_config == null)
            {
                return Application.isPlaying ? CurrentPosition : (Vector2)transform.position;
            }

            if (!Application.isPlaying)
            {
                Vector2 editorBase = HasActivePlayer ? GetPlayerPosition() : (Vector2)transform.position;
                return editorBase + Vector2.up * _config.attackTargetHeightOffset;
            }

            if (!_anchorInitialized)
            {
                Vector2 initialBase = HasActivePlayer ? GetPlayerPosition() : CurrentPosition;
                _predictedAnchor = initialBase;
                _predictedAnchorVelocity = Vector2.zero;
                _anchorInitialized = true;
            }

            return _predictedAnchor + Vector2.up * _config.attackTargetHeightOffset;
        }

        private static Vector2 EvaluateQuadraticBezier(Vector2 start, Vector2 control, Vector2 end, float t)
        {
            float u = 1f - t;
            return u * u * start + 2f * u * t * control + t * t * end;
        }

        /// <summary>
        /// Called from the attack animation event to apply damage once per dive.
        /// </summary>
        public void OnDiveAttackHit()
        {
            if (_hasDealtDamage)
                return;

            if (!CheckPlayerInRange())
                return;

            base.DealDamage();
            _hasDealtDamage = true;
        }

        private void UpdateFacing(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.001f)
                return;

            direction.Normalize();
            const float velocityFlipThreshold = 0.2f;
            const float directionFlipThreshold = 0.35f;
            const float flipHoldTime = 0.12f;
            const float maxRoll = 75f;
            const float rollSmoothTime = 0.06f;

            float velocityX = Rigidbody.linearVelocity.x;
            bool hasVelocityBias = Mathf.Abs(velocityX) > velocityFlipThreshold;
            float movementX = hasVelocityBias ? velocityX : direction.x;
            float movementAbs = Mathf.Abs(movementX);

            if (movementAbs > directionFlipThreshold)
            {
                bool wantsRight = movementX > 0f;
                if (wantsRight != _isFacingRight)
                {
                    _facingFlipTimer += Time.deltaTime;
                    if (_facingFlipTimer >= flipHoldTime)
                    {
                        _isFacingRight = wantsRight;
                        _facingFlipTimer = 0f;
                    }
                }
                else
                {
                    _facingFlipTimer = 0f;
                }
            }
            else
            {
                _facingFlipTimer = 0f;
            }

            float desiredYaw = _isFacingRight ? 0f : 180f;

            float adjustedX = hasVelocityBias ? Mathf.Abs(velocityX) : Mathf.Abs(direction.x);
            if (adjustedX < velocityFlipThreshold)
            {
                adjustedX = velocityFlipThreshold;
                float fallbackX = Mathf.Max(Mathf.Abs(direction.x), 0.01f);
                direction.x = _isFacingRight ? fallbackX : -fallbackX;
            }

            float rollDenominator = Mathf.Max(MinTimeStep, adjustedX);
            float rawRoll = Mathf.Atan2(direction.y, rollDenominator) * Mathf.Rad2Deg;
            if (desiredYaw > 90f)
            {
                rawRoll = -rawRoll;
            }
            float desiredRoll = Mathf.Clamp(rawRoll, -maxRoll, maxRoll);

            if (!Mathf.Approximately(Mathf.DeltaAngle(_smoothedYaw, desiredYaw), 0f))
            {
                _smoothedYaw = desiredYaw;
            }
            _smoothedRoll = Mathf.SmoothDampAngle(_smoothedRoll, desiredRoll, ref _rollVelocity, rollSmoothTime);

            transform.localRotation = Quaternion.Euler(0f, _smoothedYaw, _smoothedRoll);
        }

        // Показувати зону тільки коли вибрано (щоб не заважала в грі)
        private void OnDrawGizmos()
        {
            // Порожній метод - зона показується тільки в OnDrawGizmosSelected
        }

        private void OnDrawGizmosSelected()
        {
            FlyingEnemyConfig cfg = _config != null ? _config : (Config as FlyingEnemyConfig);
            if (cfg == null)
                return;

            // Визначаємо центр зони патрулювання (так само як в OnDrawGizmos)
            Vector2 center;

            if (Application.isPlaying)
            {
                center = _spawnPosition + cfg.patrolZoneOffset;
            }
            else
            {
                center = (Vector2)transform.position + cfg.patrolZoneOffset;
            }

            Vector3 center3D = new Vector3(center.x, center.y, 0f);
            Vector3 size3D = new Vector3(cfg.patrolZoneSize.x, cfg.patrolZoneSize.y, 0.01f);

            // Заповнена зона
            Gizmos.color = new Color(0f, 1f, 1f, 0.15f);
            Gizmos.DrawCube(center3D, size3D);

            // Яскрава рамка
            Gizmos.color = new Color(0f, 1f, 1f, GizmoAlpha);
            Gizmos.DrawWireCube(center3D, size3D);

            // Показуємо ГРАВЦЯ (синя сфера)
            if (Application.isPlaying && HasActivePlayer)
            {
                Vector3 playerWorldPos = Player.position;
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(playerWorldPos, 0.5f);
                Gizmos.DrawLine(transform.position, playerWorldPos);

                // Лейбл з відстанню
                float dist = Vector2.Distance(transform.position, playerWorldPos);
#if UNITY_EDITOR
                UnityEditor.Handles.Label(playerWorldPos + Vector3.up * 0.8f,
                    $"ГРАВЕЦЬ\n{dist:F1}м",
                    new GUIStyle()
                    {
                        normal = new GUIStyleState() { textColor = Color.cyan },
                        fontSize = 12,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter
                    });
#endif

                if (_config.enablePredictiveTargeting && _anchorInitialized)
                {
                    Vector3 playerPos3 = playerWorldPos;
                    Vector3 predictedBase3 = new Vector3(_predictedAnchor.x, _predictedAnchor.y, 0f);
                    Gizmos.color = new Color(0.25f, 0.85f, 1f, 0.7f);
                    Gizmos.DrawLine(playerPos3, predictedBase3);
                    Gizmos.DrawSphere(predictedBase3, 0.12f);
                }
            }

            // Показуємо цільову точку патрулювання (жовта сфера - маленька)
            if (Application.isPlaying && _state == FlightState.Patrolling)
            {
                Gizmos.color = new Color(1f, 1f, 0f, 0.6f);
                Gizmos.DrawWireSphere(_patrolTarget, 0.2f);

                // Пунктирна лінія до patrol target
                Vector3 start = transform.position;
                Vector3 end = _patrolTarget;
                float segments = 10f;
                for (int i = 0; i < segments; i += 2)
                {
                    Vector3 p1 = Vector3.Lerp(start, end, i / segments);
                    Vector3 p2 = Vector3.Lerp(start, end, Mathf.Min(i + 1, segments) / segments);
                    Gizmos.DrawLine(p1, p2);
                }
            }

            // Показуємо стан птаха
            if (Application.isPlaying)
            {
                string stateText = _state switch
                {
                    FlightState.Patrolling => "ПАТРУЛЮЄ",
                    FlightState.PreparingDive => "ГОТУЄТЬСЯ АТАКУВАТИ",
                    FlightState.Diving => "АТАКУЄ!",
                    FlightState.Returning => "ПОВЕРТАЄТЬСЯ",
                    _ => "?"
                };

#if UNITY_EDITOR
                UnityEditor.Handles.Label(transform.position + Vector3.up * 1.2f,
                    stateText,
                    new GUIStyle()
                    {
                        normal = new GUIStyleState() { textColor = Color.white },
                        fontSize = 14,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter
                    });
#endif
            }

            // Draw dive start position during preparation
            if (Application.isPlaying && _state == FlightState.PreparingDive)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(_diveStartPosition, 0.4f);
                Gizmos.DrawLine(transform.position, _diveStartPosition);
            }

            // Attack geometry preview (entry/exit points relative to гравця/спавну)
            Vector2 attackAnchor = Application.isPlaying ? GetCurrentAttackAnchor() : (Vector2)transform.position + Vector2.up * cfg.attackTargetHeightOffset;

            float horizontalDistance = Mathf.Abs(cfg.attackHorizontalDistance);
            Vector2 entryOffset = new Vector2(horizontalDistance, cfg.attackEntryHeight);
            Vector2 exitOffset = new Vector2(horizontalDistance, cfg.attackExitHeight);

            Vector3 entryRight = new Vector3(attackAnchor.x + entryOffset.x, attackAnchor.y + entryOffset.y, 0f);
            Vector3 entryLeft = new Vector3(attackAnchor.x - entryOffset.x, attackAnchor.y + entryOffset.y, 0f);
            Vector3 exitRight = new Vector3(attackAnchor.x + exitOffset.x, attackAnchor.y + exitOffset.y, 0f);
            Vector3 exitLeft = new Vector3(attackAnchor.x - exitOffset.x, attackAnchor.y + exitOffset.y, 0f);
            Vector3 anchorPoint = new Vector3(attackAnchor.x, attackAnchor.y, 0f);

            // Target point (оранжева сфера)
            Gizmos.color = new Color(1f, 0.45f, 0f, 0.7f);
            Gizmos.DrawSphere(anchorPoint, 0.18f);

            // Entry points (жовтий)
            Gizmos.color = new Color(1f, 0.85f, 0f, 0.8f);
            Gizmos.DrawWireSphere(entryRight, 0.25f);
            Gizmos.DrawWireSphere(entryLeft, 0.25f);
            Gizmos.DrawLine(entryRight, anchorPoint);
            Gizmos.DrawLine(entryLeft, anchorPoint);

            // Exit points (фіолетовий)
            Gizmos.color = new Color(0.7f, 0f, 0.9f, 0.8f);
            Gizmos.DrawWireSphere(exitRight, 0.25f);
            Gizmos.DrawWireSphere(exitLeft, 0.25f);

            // Параболічні напрямки (штрихові лінії)
            Gizmos.DrawLine(exitRight, anchorPoint);
            Gizmos.DrawLine(exitLeft, anchorPoint);

            // Якщо зараз обрана конкретна сторона - підсвітити її товщею
            if (Application.isPlaying)
            {
                Vector3 activeEntry = _attackHorizontalSign >= 0f ? entryRight : entryLeft;
                Vector3 activeExit = _attackHorizontalSign >= 0f ? exitLeft : exitRight;
                Vector3 diveTarget = new Vector3(_diveEndPosition.x, _diveEndPosition.y, 0f);

                Gizmos.color = Color.red;
                Gizmos.DrawSphere(activeEntry, 0.15f);

                Gizmos.color = Color.magenta;
                Gizmos.DrawSphere(activeExit, 0.15f);
                Gizmos.DrawLine(activeEntry, diveTarget);
                Gizmos.DrawLine(diveTarget, activeExit);
            }

            // Draw spawn position marker
            if (Application.isPlaying)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(_spawnPosition, 0.2f);
            }
        }
    }
}
