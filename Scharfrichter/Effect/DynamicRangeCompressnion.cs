using System;

namespace Scharfrichter.Codec.Effect
{
    public class FastAttackCompressor1175
    {
        private readonly int SampleRate;


        public FastAttackCompressor1175(int sampleRate)
        {
            SampleRate = sampleRate;
           
        }

        
        protected float min(float a, float b) { return Math.Min(a, b); }
        protected float max(float a, float b) { return Math.Max(a, b); }
        protected float abs(float a) { return Math.Abs(a); }
        protected float exp(float a) { return (float)Math.Exp(a); }
        protected float sqrt(float a) { return (float)Math.Sqrt(a); }
        protected float sin(float a) { return (float)Math.Sin(a); }
        protected float tan(float a) { return (float)Math.Tan(a); }
        protected float cos(float a) { return (float)Math.Cos(a); }
        protected float pow(float a, float b) { return (float)Math.Pow(a, b); }
        protected float sign(float a) { return Math.Sign(a); }
        protected float log(float a) { return (float)Math.Log(a); }
        protected float PI { get { return (float)Math.PI; } }

        float log2db;
        float db2log;
        public float attime;
        public float reltime;
        float ratio;
        float cratio;
        float rundb;
        float overdb;
        float ratatcoef;
        float ratrelcoef;
        float atcoef;
        float relcoef;
        float mix;
        float gr_meter_decay;

        public void Init()
        {
            log2db = 8.6858896380650365530225783783321f; // 20 / ln(10)
            db2log = 0.11512925464970228420089957273422f; // ln(10) / 20 
            attime = 0.010f;
            reltime = 0.100f;
            ratio = 0;
            cratio = 0;
            rundb = 0;
            overdb = 0;
            ratatcoef = exp(-1 / (0.00001f * SampleRate));
            ratrelcoef = exp(-1 / (0.5f * SampleRate));
            atcoef = exp(-1 / (attime * SampleRate));
            relcoef = exp(-1 / (reltime * SampleRate));
            mix = 1;
            gr_meter = 1;
            gr_meter_decay = exp(1 / (1 * SampleRate));
            
        }

        public float thresh;
        float threshv;
        float allin;
        float cthresh;
        float cthreshv;
        float softknee = 0; // never assigned to
        public float makeup;
        float makeupv;
        float autogain = 0; // never assigned to

        public void Apply()
        {
            threshv = exp(thresh * db2log);
            ratio = (1 == 0 ? 4 : (1 == 1 ? 8 : (1 == 2 ? 12 : (1 == 3 ? 20 : 20))));
            if (1 == 4) { allin = 1; cratio = 20; } else { allin = 0; cratio = ratio; }
            cthresh = (softknee != 0) ? (thresh - 3) : thresh;
            cthreshv = exp(cthresh * db2log);
            makeupv = exp((makeup + autogain) * db2log);
            attime = 20 / 1000000;
            reltime = 250 / 1000;
            atcoef = exp(-1 / (attime * SampleRate));
            relcoef = exp(-1 / (reltime * SampleRate));
            mix = 100 / 100;
        }

        float rmscoef = 0; // never assigned to
        float averatio;
        float runratio;
        float maxover;
        float gr_meter;
        float runmax;
        float runave;

        public void Sample(ref float spl0, ref float spl1)
        {
            float ospl0 = spl0;
            float ospl1 = spl1;
            float aspl0 = Math.Abs(spl0);
            float aspl1 = Math.Abs(spl1);
            float maxspl = Math.Max(aspl0, aspl1);
            maxspl = maxspl * maxspl;
            runave = maxspl + rmscoef * (runave - maxspl);
            float det = sqrt(max(0, runave));

            overdb = 2.08136898f * log(det / cthreshv) * log2db;
            overdb = Math.Max(0, overdb);

            if (overdb - rundb > 5) averatio = 4;

            if (overdb > rundb)
            {
                rundb = overdb + atcoef * (rundb - overdb);
                runratio = averatio + ratatcoef * (runratio - averatio);
            }
            else
            {
                rundb = overdb + relcoef * (rundb - overdb);
                runratio = averatio + ratrelcoef * (runratio - averatio);
            }
            overdb = rundb;
            averatio = runratio;

            if (allin != 0)
            {
                cratio = 12 + averatio;
            }
            else
            {
                cratio = ratio;
            }

            float gr = -overdb * (cratio - 1) / cratio;
            float grv = exp(gr * db2log);

            runmax = maxover + relcoef * (runmax - maxover);  // highest peak for setting att/rel decays in reltime
            maxover = runmax;

            if (grv < gr_meter) gr_meter = grv; else { gr_meter *= gr_meter_decay; if (gr_meter > 1) gr_meter = 1; };

            spl0 *= grv * makeupv * mix;
            spl1 *= grv * makeupv * mix;

            spl0 += ospl0 * (1 - mix);
            spl1 += ospl1 * (1 - mix);
        }

    }
}