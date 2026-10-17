using UnityEngine;

namespace MojiBattle
{
    /// <summary>Stage mechanics advance inside the match's manual physics step, including silent tournament matches.</summary>
    public sealed class ArenaStage : MonoBehaviour
    {
        public BattleStageId Id => director.Config.stage;
        public string Notice { get; private set; }
        public float Tilt { get; private set; }
        public float HazardX { get; private set; }
        public float HazardY { get; private set; }
        public float Phase { get; private set; }
        public int Activations { get; private set; }
        public float HealingX { get; private set; }
        public bool Warning { get; private set; }
        public bool ObstacleActive { get; private set; }
        public float ConveyorDirection { get; private set; }
        MatchDirector director;
        Transform ground;
        Rigidbody2D groundBody;
        PhysicsMaterial2D groundMaterial;
        BoxCollider2D obstacle;
        Rigidbody2D obstacleBody;
        ArenaStageView view;
        int firedCycle=-1;
        bool presenting;

        public void Bind(MatchDirector d, Transform arena, bool presentation)
        {
            director=d; presenting=presentation;
            ground=arena.Find("Ground");
            groundMaterial=new PhysicsMaterial2D("StageGround") { friction=Id==BattleStageId.Calligraphy?.08f:.6f, bounciness=0 };
            ground.GetComponent<BoxCollider2D>().sharedMaterial=groundMaterial;
            if(Id==BattleStageId.Calligraphy)
            {
                groundBody=ground.gameObject.AddComponent<Rigidbody2D>();groundBody.bodyType=RigidbodyType2D.Kinematic;
            }
            if(Id==BattleStageId.EraserFactory || Id==BattleStageId.Letterpress)
            {
                var go=new GameObject("StageObstacle");go.transform.SetParent(transform,false);
                obstacle=go.AddComponent<BoxCollider2D>();obstacle.size=Id==BattleStageId.EraserFactory?new Vector2(1.5f,2.1f):new Vector2(1.6f,1.6f);
                obstacle.sharedMaterial=groundMaterial;go.AddComponent<ArenaSurface>().Kind=ArenaSurface.SurfaceKind.Ground;
                obstacleBody=go.AddComponent<Rigidbody2D>();obstacleBody.bodyType=RigidbodyType2D.Kinematic;obstacle.enabled=false;
            }
            if(presentation && Id!=BattleStageId.Dojo) { view=gameObject.AddComponent<ArenaStageView>();view.Bind(this,d); }
            Evaluate(0,false);
        }
        public float GroundAt(float x) => Id==BattleStageId.Calligraphy ? -1+Mathf.Cos(Tilt*Mathf.Deg2Rad)+Mathf.Tan(Tilt*Mathf.Deg2Rad)*x : 0;
        public void Step(float dt)
        {
            if(!director.Fighting || director.Context.MatchOver)return;
            Evaluate(dt,true);
        }
        public void RefreshRemote() => Evaluate(0,false);

        void Evaluate(float dt, bool simulate)
        {
            float t=director.Clock;
            int cycle=Mathf.FloorToInt(t/10f);
            Phase=t-cycle*10;
            Warning=false;Notice="";
            float half=director.Context.ArenaHalfWidth;
            // A stable integer hash gives both clients and headless simulations the same hazard positions.
            uint hash=unchecked((uint)director.Config.seed*1664525u+(uint)cycle*1013904223u);
            HazardX=Mathf.Lerp(-half*.55f,half*.55f,(hash%1000)/999f);
            HealingX=(cycle%2==0?-1:1)*half*.38f;
            if(Id==BattleStageId.Calligraphy)
            {
                Tilt=3.5f*Mathf.Sin(t*Mathf.PI/6);
                if(groundBody!=null) { if(simulate)groundBody.MoveRotation(Tilt);else ground.rotation=Quaternion.Euler(0,0,Tilt); }
                Warning=Phase<2.5f;if(presenting)Notice=Tilt<0?"右へ傾斜 ／ 墨だまりは滑る":"左へ傾斜 ／ 墨だまりは滑る";
                if(simulate)foreach(var f in director.Fighters)
                    if(f.IsGrounded && Mathf.Abs(f.X-half*.27f)<2.5f) f.AddVelocity(new Vector2(-Mathf.Sign(Tilt)*2.2f*dt,0));
            }
            else if(Id==BattleStageId.EraserFactory || Id==BattleStageId.Letterpress)
            {
                Warning=Phase<3;
                if(presenting)Notice=Warning?$"{(Id==BattleStageId.EraserFactory?"消しゴム":"活字")}落下まで {Mathf.CeilToInt(3-Phase)}":Phase<7?"落下物が足場になる":"次の落下に備えよう";
                ObstacleActive=Phase>=3 && Phase<7;
                float rest=obstacle.size.y/2;
                HazardY=Mathf.Lerp(9,rest,Mathf.Clamp01((Phase-3)/.8f));
                obstacle.enabled=ObstacleActive;
                obstacleBody.position=new Vector2(HazardX,HazardY);
                if(simulate && Phase>=3.8f && Phase<7 && firedCycle!=cycle)
                {
                    firedCycle=cycle;Activations++;
                    foreach(var f in director.Fighters)
                    {
                        if(Mathf.Abs(f.X-HazardX)>1.5f || f.Body.position.y>3.2f)continue;
                        Damage(f,3.5f);
                        var delta=new Vector2(0,Mathf.Max(0,obstacle.size.y+.18f-f.Body.position.y));
                        f.Body.position+=delta;if(!f.Runtime.weaponDetached)f.WeaponBody.position+=delta;
                        f.AddVelocity(new Vector2(Mathf.Sign(f.X-HazardX)*1.3f,1.5f));
                    }
                }
                if(Id==BattleStageId.EraserFactory)
                {
                    ConveyorDirection=cycle%2==0?1:-1;
                    if(simulate)foreach(var f in director.Fighters)if(f.IsGrounded && f.Body.position.y<1.1f)f.AddVelocity(new Vector2(ConveyorDirection*2.8f*dt,0));
                }
            }
            else if(Id==BattleStageId.Dictionary)
            {
                Warning=Phase<3;
                if(presenting)Notice=Warning?$"ページめくりまで {Mathf.CeilToInt(3-Phase)}":Phase<4.5f?"ページがめくれた！":"次のページに備えよう";
                if(simulate && Phase>=3 && firedCycle!=cycle)
                {
                    firedCycle=cycle;Activations++;
                    foreach(var f in director.Fighters)
                        if(!f.Runtime.IsDown)f.AddVelocity(new Vector2((cycle%2==0?1:-1)*1.3f,4.5f));
                }
            }
            else if(Id==BattleStageId.Exam)
            {
                HazardX=-HealingX;Warning=Phase<3;
                if(presenting)Notice=Warning?$"採点まで {Mathf.CeilToInt(3-Phase)} ／ ○ 回復・× 斬撃":"○で回復 ／ 次の採点に備えよう";
                if(simulate)
                {
                    foreach(var f in director.Fighters)
                        if(Mathf.Abs(f.X-HealingX)<1.7f && f.Body.position.y<1.4f && !f.Runtime.IsDown)
                            f.Runtime.hp=Mathf.Min(f.Runtime.maxHp,f.Runtime.hp+dt*.65f);
                    if(Phase>=3 && firedCycle!=cycle)
                    {
                        firedCycle=cycle;Activations++;
                        foreach(var f in director.Fighters)if(Mathf.Abs(f.X-HazardX)<1.8f && f.Body.position.y<3)
                        {Damage(f,4);f.AddVelocity(new Vector2(-Mathf.Sign(HazardX)*1.3f,2));}
                    }
                }
            }
            view?.Draw();
        }
        void Damage(Fighter f,float amount)
        {
            if(f.Runtime.invulnerable || f.Runtime.hp<=0)return;
            var ctx=director.Context;float damage=Mathf.Min(amount,f.Runtime.hp);
            f.Runtime.ApplyDamage(damage);f.Runtime.metrics.envDamageTaken+=damage;f.Runtime.metrics.envImpacts++;
            ctx.LastDamageTime=ctx.SimTime;
            ctx.Events.Raise(new EnvImpactEvent {fighter=f.Id,damage=damage,point=f.Body.position,time=ctx.SimTime});
            if(f.Runtime.hp<=0)f.Knockdown.EnterKO(ctx.SimTime);
        }
        void OnDestroy()
        {
            if(groundMaterial!=null)Destroy(groundMaterial);
        }
    }
}
