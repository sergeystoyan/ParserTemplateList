//********************************************************************************************
//Author: Sergey Stoyan
//        sergey.stoyan@gmail.com
//        http://www.cliversoft.com
//********************************************************************************************
using System;
using System.Text.RegularExpressions;
using Cliver.PdfDocumentParser;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;

namespace Cliver.PdfDocumentParserTemplateList
{
    public class Template2
    {
        public Template2 Clone()
        {
            return Serialization.Json.Clone(this);
        }

        public Template Template;

        public bool Active = true;
        public string Group = "";
        public DateTime ModifiedTime;
        public string Comment = "";
        public float OrderWeight = 1f;
        public Regex FileFilterRegex = null;
        public string DocumentParserClass = "";
        public string DocumentParserClassDefinition = "";
        //public string Company;

        public string GetModifiedTimeAsString()
        {
            return ModifiedTime.ToString("yy-MM-dd HH:mm:ss");
        }

        public virtual void Rectify(Template2 initialTemplate)
        {
            Template2 t0 = initialTemplate;
            //for (int i = t.Template.Conditions.Count - 1; i >= 0; i--)
            //    if (t0.Template.Conditions.Find(a => a.Name == t.Template.Conditions[i].Name) == null)
            //        t.Template.Conditions.RemoveAt(i);
            for (int i = 0; i < t0.Template.Conditions.Count; i++)
                if (Template.Conditions.Find(a => a.Name == t0.Template.Conditions[i].Name) == null)
                    Template.Conditions.Insert(i, t0.Template.Conditions[i]);
            for (int i = 0; i < t0.Template.Conditions.Count; i++)//ordering
                while (Template.Conditions[i].Name != t0.Template.Conditions[i].Name)
                {
                    Template.Conditions.Add(Template.Conditions[i]);
                    Template.Conditions.RemoveAt(i);
                }

            //for (int i = t.Template.Fields.Count - 1; i >= 0; i--)
            //    if (t0.Template.Fields.Find(a => a.Name == t.Template.Fields[i].Name) == null)
            //        t.Template.Fields.RemoveAt(i);
            for (int i = 0; i < t0.Template.Fields.Count; i++)
                if (Template.Fields.Find(a => a.Name == t0.Template.Fields[i].Name) == null)
                    Template.Fields.Add(t0.Template.Fields[i]);
            int j = 0;
            for (int i = 0; i < t0.Template.Fields.Count; i++)//ordering
            {
                while (Template.Fields[j].Name != t0.Template.Fields[i].Name)
                {
                    Template.Fields.Add(Template.Fields[j]);
                    Template.Fields.RemoveAt(j);
                }
                j++;
                while (j < Template.Fields.Count && Template.Fields[j].Name == t0.Template.Fields[i].Name)//there may be multiple field defintions
                    j++;
            }
        }
    }
}