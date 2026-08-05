using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProceduralAssembly
{
    public sealed class ProceduralAssemblyDemo : MonoBehaviour
    {
        private sealed class Knight
        {
            public GameObject root;
            public GameObject model;
            public Animator animator;
            public float health=100, attackCooldown, hurtTime, deathTime;
            public Vector3 velocity, recoil;
            public bool dead, player;
            public readonly List<Transform> reactionBones=new();
        }

        private readonly List<Knight> knights=new();
        private readonly List<GameObject> spawned=new();
        private Knight player;
        private Camera fpCamera;
        private Transform viewSword;
        private float yaw, pitch, attackTime, shake, vignette;
        private float cameraEyeHeight=1.68f;
        private Texture2D pixel;
        private Material flameBlue, flameMagenta, flameAmber, blackFloor;
        private readonly string[] dungeonPieces={"ModularFloor","ModularStoneWall","ModularStoneWall_top","Column","Column_Broken","Column_Broken2","Entrance","Entrance2","WallRocks","Bars","Chest","Barrel"};

        // Intentionally not registered with RuntimeInitializeOnLoadMethod.
        // Add this component to a dedicated Broken Edge scene when that prototype should run.
        // Asset Store demo scenes must be able to enter Play Mode without this environment being injected.
        private static void BootstrapBrokenEdgePrototype()
        {
            if(FindFirstObjectByType<ProceduralAssemblyDemo>()==null)
                new GameObject("Broken Edge First Person").AddComponent<ProceduralAssemblyDemo>();
        }

        private void Awake()
        {
            Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;Application.targetFrameRate=120;
            pixel=new Texture2D(1,1);pixel.SetPixel(0,0,Color.white);pixel.Apply();
            BuildMaterials();BuildDungeon();BuildLighting();SpawnCharacters();
        }

        private void BuildMaterials()
        {
            flameBlue=MaterialOf("Arcane Blue",new Color(.03f,.25f,.75f),new Color(.02f,.3f,1f)*5);
            flameMagenta=MaterialOf("Duel Magenta",new Color(.45f,.025f,.2f),new Color(1f,.015f,.32f)*4);
            flameAmber=MaterialOf("Royal Amber",new Color(.7f,.18f,.025f),new Color(1f,.13f,.015f)*5);
            blackFloor=MaterialOf("Black Stone",new Color(.018f,.022f,.027f),Color.black);
        }

        private Material MaterialOf(string name,Color albedo,Color emission)
        {
            var s=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");var m=new Material(s){name=name,color=albedo};
            m.SetFloat("_Glossiness",.22f);if(emission.maxColorComponent>0){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",emission);}return m;
        }

        private void BuildDungeon()
        {
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.018f,.025f,.04f);RenderSettings.ambientEquatorColor=new Color(.012f,.015f,.025f);RenderSettings.ambientGroundColor=Color.black;
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.028f;RenderSettings.fogColor=new Color(.008f,.012f,.022f);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Blackstone Foundation";floor.transform.position=new Vector3(0,-.55f,5);floor.transform.localScale=new Vector3(26,1,34);floor.GetComponent<Renderer>().sharedMaterial=blackFloor;

            for(int z=-2;z<7;z++)for(int x=-3;x<4;x++) Place("ModularFloor",new Vector3(x*3.5f,0,z*3.5f),Quaternion.identity,2.7f);
            for(int z=-2;z<7;z++)
            {
                Place(z%3==0?"Column_Broken":"ModularStoneWall",new Vector3(-11.8f,1.6f,z*3.5f),Quaternion.Euler(0,90,0),2.7f);
                Place(z%2==0?"Column":"ModularStoneWall",new Vector3(11.8f,1.6f,z*3.5f),Quaternion.Euler(0,-90,0),2.7f);
            }
            for(int x=-3;x<4;x++){if(x!=0)Place("ModularStoneWall",new Vector3(x*3.5f,1.6f,25.5f),Quaternion.identity,2.7f);}
            Place("Entrance2",new Vector3(0,0,25.5f),Quaternion.identity,3.2f);
            Place("Entrance",new Vector3(0,0,-8f),Quaternion.Euler(0,180,0),3.2f);
            for(int i=0;i<10;i++)
            {
                float side=i%2==0?-1:1;float z=-3+i*3.1f;
                Place(i%3==0?"Chest":"Barrel",new Vector3(side*9.4f,0,z),Quaternion.Euler(0,i*37,0),2.1f);
                if(i%2==0)Place("Torch_wall",new Vector3(side*10.7f,2.35f,z+1),Quaternion.Euler(0,side<0?90:-90,0),2.2f);
            }
        }

        private GameObject Place(string resource,Vector3 pos,Quaternion rot,float scale)
        {
            var prefab=Resources.Load<GameObject>("CC0/"+resource);if(prefab==null)return null;
            var go=Instantiate(prefab,pos,rot);go.name="CC0 "+resource;go.transform.localScale=Vector3.one*scale;spawned.Add(go);
            if(resource.Contains("Wall")||resource.Contains("Entrance")||resource.Contains("Column")||resource=="Bars")
            {var c=go.AddComponent<BoxCollider>();c.center=Vector3.up*.7f;c.size=new Vector3(1,2.5f,1);}
            return go;
        }

        private void BuildLighting()
        {
            foreach(var l in FindObjectsByType<Light>(FindObjectsSortMode.None))l.gameObject.SetActive(false);
            MakeLight("Cold Moon Shafts",LightType.Directional,new Vector3(48,-32,12),new Color(.25f,.42f,1f),.42f,0,true);
            var specs=new[]{
                (new Vector3(-7,3,-1),new Color(.03f,.24f,1f),flameBlue),
                (new Vector3(7,2.7f,5),new Color(1f,.015f,.25f),flameMagenta),
                (new Vector3(-6,2.8f,12),new Color(1f,.2f,.025f),flameAmber),
                (new Vector3(6,3,18),new Color(.04f,.28f,1f),flameBlue),
                (new Vector3(0,4,24),new Color(1f,.03f,.18f),flameMagenta)};
            foreach(var s in specs){MakeLight("Saturated Duel Light",LightType.Point,s.Item1,s.Item2,7.5f,10,false);var orb=GameObject.CreatePrimitive(PrimitiveType.Sphere);orb.name="Arcane Flame";orb.transform.position=s.Item1;orb.transform.localScale=new Vector3(.18f,.48f,.18f);orb.GetComponent<Renderer>().sharedMaterial=s.Item3;Destroy(orb.GetComponent<Collider>());}
        }

        private void MakeLight(string name,LightType type,Vector3 p,Color color,float intensity,float range,bool shadows)
        {
            var g=new GameObject(name);var l=g.AddComponent<Light>();l.type=type;l.color=color;l.intensity=intensity;l.range=range;l.shadows=shadows?LightShadows.Soft:LightShadows.None;
            if(type==LightType.Directional)g.transform.rotation=Quaternion.Euler(p);else g.transform.position=p;
        }

        private void SpawnCharacters()
        {
            player=SpawnKnight(new Vector3(0,0,-4),true);
            fpCamera=Camera.main;if(fpCamera==null){var c=new GameObject("First Person Camera");c.tag="MainCamera";fpCamera=c.AddComponent<Camera>();}
            fpCamera.transform.SetParent(player.root.transform);
            var playerSkin=player.model.GetComponentInChildren<SkinnedMeshRenderer>();
            cameraEyeHeight=playerSkin!=null?Mathf.Max(1.55f,(playerSkin.bounds.max.y-player.root.transform.position.y)*.94f):1.68f;
            fpCamera.transform.localPosition=new Vector3(0,cameraEyeHeight,.22f);fpCamera.nearClipPlane=.025f;fpCamera.fieldOfView=67;fpCamera.clearFlags=CameraClearFlags.SolidColor;fpCamera.backgroundColor=Color.black;
            var viewWeapon=Instantiate(Resources.Load<GameObject>("CC0/Sword"),fpCamera.transform);viewWeapon.name="FIRST PERSON • RIGHT HAND SWORD";
            viewSword=viewWeapon.transform;viewSword.localPosition=new Vector3(.46f,-.42f,.82f);viewSword.localRotation=Quaternion.Euler(-18f,8f,-12f);viewSword.localScale=Vector3.one*.72f;
            SpawnKnight(new Vector3(-4,0,6),false);SpawnKnight(new Vector3(4,0,11),false);SpawnKnight(new Vector3(0,0,19),false);
        }

        private Knight SpawnKnight(Vector3 pos,bool isPlayer)
        {
            var k=new Knight{player=isPlayer};k.root=new GameObject(isPlayer?"PLAYER • IVORY KNIGHT":"DUELIST • HUMANOID");k.root.transform.position=pos;
            var prefab=Resources.Load<GameObject>("CC0/KnightCharacter");k.model=Instantiate(prefab,k.root.transform);k.model.name="Animated Humanoid Avatar";k.model.transform.localPosition=Vector3.zero;k.model.transform.localRotation=Quaternion.identity;
            k.animator=k.model.GetComponentInChildren<Animator>();if(k.animator==null)k.animator=k.model.AddComponent<Animator>();k.animator.runtimeAnimatorController=Resources.Load<RuntimeAnimatorController>("CC0/KnightCombat");k.animator.applyRootMotion=false;
            foreach(var bone in k.model.GetComponentsInChildren<Transform>(true))
            {
                string n=bone.name.ToLowerInvariant();
                if(n.Contains("spine")||n.Contains("chest")||n.Contains("head")||n.Contains("upperarm")||n.Contains("shoulder"))k.reactionBones.Add(bone);
            }
            var hand=FindDeep(k.model.transform,"Palm.R")??FindContains(k.model.transform,"hand_r")??k.model.transform;
            var swordPrefab=Resources.Load<GameObject>("CC0/Sword");var sword=Instantiate(swordPrefab,hand);sword.name="Sword • Right Hand";sword.transform.localPosition=Vector3.zero;sword.transform.localRotation=Quaternion.Euler(0,0,0);sword.transform.localScale=Vector3.one;
            if(!isPlayer){var cc=k.root.AddComponent<CapsuleCollider>();cc.height=1.8f;cc.radius=.4f;cc.center=Vector3.up*.9f;}
            knights.Add(k);return k;
        }

        private Transform FindDeep(Transform root,string exact){foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t.name==exact)return t;return null;}
        private Transform FindContains(Transform root,string part){foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t.name.ToLowerInvariant().Contains(part.ToLowerInvariant()))return t;return null;}

        private void Update()
        {
            float dt=Mathf.Min(Time.deltaTime,.04f);if(Keyboard.current?.escapeKey.wasPressedThisFrame==true){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
            UpdateLook();UpdatePlayer(dt);UpdateEnemies(dt);UpdateCombat(dt);UpdateCamera(dt);
        }

        private void UpdateLook()
        {
            if(Cursor.lockState!=CursorLockMode.Locked||Mouse.current==null)return;Vector2 d=Mouse.current.delta.ReadValue();yaw+=d.x*.075f;pitch=Mathf.Clamp(pitch-d.y*.065f,-72,72);
            player.root.transform.rotation=Quaternion.Euler(0,yaw,0);fpCamera.transform.localRotation=Quaternion.Euler(pitch,0,0);
        }

        private void UpdatePlayer(float dt)
        {
            if(player.dead)return;Vector2 input=Vector2.zero;var k=Keyboard.current;if(k!=null){if(k.wKey.isPressed)input.y++;if(k.sKey.isPressed)input.y--;if(k.aKey.isPressed)input.x--;if(k.dKey.isPressed)input.x++;}
            if(input.sqrMagnitude>1)input.Normalize();Vector3 move=player.root.transform.TransformDirection(new Vector3(input.x,0,input.y));float speed=(k?.leftShiftKey.isPressed??false)?6.2f:3.8f;
            player.velocity=Vector3.Lerp(player.velocity,move*speed,1-Mathf.Exp(-12*dt));player.root.transform.position+=player.velocity*dt;player.animator.SetFloat("Speed",input.magnitude);
            if((Mouse.current?.leftButton.wasPressedThisFrame??false)&&attackTime<=0)PlayerAttack();
        }

        private void PlayerAttack()
        {
            attackTime=.66f;player.animator.ResetTrigger("Attack");player.animator.CrossFade("Attack",.035f,0,0f);shake=.11f;
            Knight best=null;float bestDist=3.2f;foreach(var e in knights){if(e.player||e.dead)continue;Vector3 to=e.root.transform.position+Vector3.up-fpCamera.transform.position;float d=to.magnitude;if(d<bestDist&&Vector3.Dot(fpCamera.transform.forward,to/d)>.72f){best=e;bestDist=d;}}
            if(best!=null)Hit(best,fpCamera.transform.forward,36);
        }

        private void UpdateEnemies(float dt)
        {
            foreach(var e in knights)
            {
                if(e.player||e.dead)continue;e.attackCooldown-=dt;e.hurtTime-=dt;Vector3 to=player.root.transform.position-e.root.transform.position;to.y=0;float d=to.magnitude;
                if(e.hurtTime>0){e.root.transform.position+=e.recoil*dt;e.recoil=Vector3.Lerp(e.recoil,Vector3.zero,dt*7);continue;}
                if(!player.dead&&d>1.65f){Vector3 dir=to/Mathf.Max(d,.01f);e.root.transform.position+=dir*2.15f*dt;e.root.transform.rotation=Quaternion.Slerp(e.root.transform.rotation,Quaternion.LookRotation(dir),dt*7);e.animator.SetFloat("Speed",1);}
                else{e.animator.SetFloat("Speed",0);if(!player.dead&&e.attackCooldown<=0){e.attackCooldown=1.35f;e.animator.CrossFade("Attack",.045f,0,0f);Hit(player,to.normalized,14);}}
            }
        }

        private void Hit(Knight target,Vector3 direction,float damage)
        {
            target.health-=damage;target.hurtTime=.32f;target.recoil=direction*-(target.player?2.5f:5.5f);vignette=target.player?.7f:vignette;shake=Mathf.Max(shake,.22f);
            if(!target.player)target.animator.CrossFade("Hit",.035f,0,0f);
            if(target.health<=0){target.dead=true;target.animator.SetBool("Death",true);target.deathTime=4;target.velocity=Vector3.zero;}
        }

        private void UpdateCombat(float dt)
        {
            attackTime-=dt;vignette=Mathf.MoveTowards(vignette,0,dt*1.4f);
            foreach(var k in knights)if(k.dead){k.deathTime-=dt;if(k.deathTime<=0)Respawn(k);}
        }

        private void Respawn(Knight k)
        {
            k.dead=false;k.health=100;k.animator.SetBool("Death",false);k.root.transform.position=k.player?new Vector3(0,0,-4):new Vector3(Random.Range(-5,5),0,Random.Range(8,21));
            k.animator.Play("Idle",0,0);if(k.player){yaw=0;pitch=0;}
        }

        private void UpdateCamera(float dt)
        {
            shake=Mathf.MoveTowards(shake,0,dt*.8f);fpCamera.transform.localPosition=new Vector3(0,cameraEyeHeight,.22f)+Random.insideUnitSphere*shake;
            if(viewSword!=null)
            {
                float progress=attackTime>0?1f-Mathf.Clamp01(attackTime/.66f):0f;
                float arc=attackTime>0?Mathf.Sin(progress*Mathf.PI):0f;
                viewSword.localPosition=new Vector3(.46f,-.42f,.82f)+new Vector3(-arc*.18f,arc*.1f,arc*.18f);
                viewSword.localRotation=Quaternion.Euler(-18f+arc*58f,8f-arc*42f,-12f-arc*74f);
            }
        }

        private void LateUpdate()
        {
            // Procedural hit reaction is layered after Mecanim, so imported locomotion/attack clips remain reusable.
            foreach(var k in knights)
            {
                if(k.dead||k.hurtTime<=0)continue;
                float phase=Mathf.Clamp01(k.hurtTime/.32f);float kick=Mathf.Sin(phase*Mathf.PI);
                for(int i=0;i<k.reactionBones.Count;i++)
                {
                    Transform bone=k.reactionBones[i];float side=(i%2==0?-1f:1f);
                    bone.localRotation*=Quaternion.Euler(-kick*(i<3?13f:7f),side*kick*6f,side*kick*4f);
                }
            }
        }

        private void OnGUI()
        {
            if(player==null||pixel==null)return;GUIStyle label=new(GUI.skin.label){fontSize=15,fontStyle=FontStyle.Bold,normal={textColor=new Color(.78f,.74f,.66f)}};
            Draw(new Rect(26,26,280,6),new Color(.04f,.045f,.055f,.9f));Draw(new Rect(28,28,276*Mathf.Clamp01(player.health/100),2),new Color(.2f,.48f,1f));GUI.Label(new Rect(26,38,300,28),"IVORY KNIGHT   "+Mathf.CeilToInt(player.health),label);
            float cx=Screen.width*.5f,cy=Screen.height*.5f;Draw(new Rect(cx-8,cy,16,1),new Color(.85f,.75f,.55f,.8f));Draw(new Rect(cx,cy-8,1,16),new Color(.85f,.75f,.55f,.8f));
            GUI.Label(new Rect(26,Screen.height-48,560,26),"WASD MOVE  •  MOUSE LOOK  •  LMB STRIKE  •  SHIFT SPRINT  •  ESC CURSOR",label);
            if(vignette>0){Draw(new Rect(0,0,Screen.width,22),new Color(.65f,.01f,.05f,vignette));Draw(new Rect(0,Screen.height-22,Screen.width,22),new Color(.65f,.01f,.05f,vignette));}
        }
        private void Draw(Rect r,Color c){var old=GUI.color;GUI.color=c;GUI.DrawTexture(r,pixel);GUI.color=old;}
    }
}
