//********************************************************************************************
//Author: Sergey Stoyan
//        sergey.stoyan@gmail.com
//        sergey.stoyan@hotmail.com
//        http://www.cliversoft.com
//********************************************************************************************
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.IO;
using System.Threading;
using Cliver.PdfDocumentParser;
using System.Drawing;

namespace Cliver.ParserTemplateList
{
    abstract public partial class TemplateListControl<Template2T> : UserControl where Template2T : Template2
    {
        public abstract TemplateInfoSettings<Template2T> TemplateInfo { get; }

        public abstract LocalInfoSettings<Template2T> LocalInfo { get; }

        public abstract DebugForm<Template2T> NewDebugForm();

        public abstract Template2Form<Template2T> NewTemplate2Form(Template2T template2);

        public TemplateListControl()
        {
            InitializeComponent();

            Load += delegate { initialize(); };
        }

        void initialize()
        {
            template2s.CellPainting += delegate (object sender, DataGridViewCellPaintingEventArgs e)
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0)
                    return;
                Brush brush = null;
                if (e.ColumnIndex == 1)
                {
                    Template2 t = (Template2)template2s.Rows[e.RowIndex].Tag;
                    if (t == null)
                        return;
                    if (t.Template.Deskew != null)
                        brush = Brushes.LightPink;
                }
                if (e.ColumnIndex == 3)
                {
                    Template2 t = (Template2)template2s.Rows[e.RowIndex].Tag;
                    if (t == null)
                        return;
                    if (!string.IsNullOrWhiteSpace(t.DocumentParserClass))
                        brush = Brushes.LightCyan;
                    else if (!string.IsNullOrWhiteSpace(Compiler.RemoveComments(t.DocumentParserClassDefinition)))
                        brush = Brushes.LightYellow;
                }
                if (e.ColumnIndex == 5)
                {
                    Template2 t = (Template2)template2s.Rows[e.RowIndex].Tag;
                    if (t == null)
                        return;
                    if (t.Active)
                        brush = Brushes.LightGreen;
                }
                if (brush == null)
                    return;
                e.Paint(e.CellBounds, DataGridViewPaintParts.All & ~(DataGridViewPaintParts.ContentForeground));
                Rectangle r = e.CellBounds;
                r.Inflate(-4, -4);
                e.Graphics.FillRectangle(brush, r);
                e.Paint(e.CellBounds, DataGridViewPaintParts.ContentForeground);
                e.Handled = true;
            };

            initializeSelectionEngine();

            Active.ValueType = typeof(bool);
            Selected.ValueType = typeof(bool);
            OrderWeight.ValueType = typeof(float);

            TemplateInfoSettings<Template2T>.TouchedChanged += delegate ()
            {
                this.BeginInvoke(() =>
                {
                    saveTemplates.Enabled = TemplateInfo.IsTouched();
                });
            };

            openTemplatesSettings.Click += delegate
            {
                TemplatesSettingsForm<Template2T> f = new TemplatesSettingsForm<Template2T>(TemplateInfo);
                f.ShowDialog();
            };

            saveTemplates.Enabled = false;
            saveTemplates.Click += delegate
            {
                SaveFromGui(false);
            };

            template2s.CellValidating += delegate (object sender, DataGridViewCellValidatingEventArgs e)
            {
                try
                {
                    DataGridViewRow r = template2s.Rows[e.RowIndex];
                    Template2 t = (Template2)r.Tag;

                    switch (template2s.Columns[e.ColumnIndex].Name)
                    {
                        case "Name_":
                            {
                                if (string.IsNullOrWhiteSpace((string)e.FormattedValue))
                                {
                                    if (t != null)
                                        throw new Exception("Name cannot be empty!");
                                    return;
                                }
                                string name2 = ((string)e.FormattedValue).Trim();
                                foreach (DataGridViewRow rr in template2s.Rows)
                                {
                                    if (rr.Index != e.RowIndex && name2 == (string)rr.Cells["Name_"].Value)
                                        throw new Exception("Name '" + name2 + "' is duplicated!");
                                }
                                if ((string)r.Cells["Name_"].Value != name2)
                                    r.Cells["Name_"].Value = name2;
                            }
                            return;
                    }
                }
                catch (Exception ex)
                {
                    e.Cancel = true;
                    Message.Error2(ex, FindForm());
                }
            };

            template2s.DataError += delegate (object sender, DataGridViewDataErrorEventArgs e)
            {
                try
                {
                    DataGridViewRow r = template2s.Rows[e.RowIndex];
                    Template2 t = (Template2)r.Tag;

                    switch (template2s.Columns[e.ColumnIndex].Name)
                    {
                        case "OrderWeight":
                            throw new Exception("Order must be a float number:\r\n" + e.Exception.Message);
                        case "DetectingTemplateLastPageNumber":
                            throw new Exception("DetectingTemplateLastPageNumber must be a uint number:\r\n" + e.Exception.Message);
                        case "FileFilterRegex":
                            throw new Exception("FileFilterRegex must be a regex.");
                        case "SharedFileTemplateNamesRegex":
                            throw new Exception("SharedFileTemplateNamesRegex must be a regex.");
                    }
                }
                catch (Exception ex)
                {
                    Message.Error2(ex, FindForm());
                }
            };

            template2s.CellValueChanged += delegate (object sender, DataGridViewCellEventArgs e)
            {
                try
                {
                    DataGridViewRow r = template2s.Rows[e.RowIndex];
                    Template2 t = (Template2)r.Tag;
                    if (t == null)
                        return;
                    if (e.ColumnIndex < 0)//row's header
                        return;

                    switch (template2s.Columns[e.ColumnIndex].Name)
                    {
                        case "Name_":
                            t.Name = (string)r.Cells["Name_"].Value;
                            break;
                        case "Active":
                            t.Active = (bool)r.Cells["Active"].Value;
                            break;
                        case "Comment":
                            t.Comment = (string)r.Cells["Comment"].Value;
                            break;
                        case "Group":
                            t.Group = (string)r.Cells["Group"].Value;
                            break;
                        case "OrderWeight":
                            t.OrderWeight = (float)r.Cells["OrderWeight"].Value;
                            break;
                    }
                    TemplateInfo.Touch();
                }
                catch (Exception ex)
                {
                    Message.Error2(ex, FindForm());
                }
            };

            template2s.UserDeletingRow += delegate (object sender, DataGridViewRowCancelEventArgs e)
            {
                try
                {
                    if (e.Row == null || e.Row.Tag == null)
                        return;
                    if (!Message.YesNo("Template '" + e.Row.Cells["Name_"].Value + "' will be deleted! Proceed?", FindForm()))
                    {
                        e.Cancel = true;
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                    Message.Error(ex, FindForm());
                }
            };

            template2s.UserDeletedRow += delegate (object sender, DataGridViewRowEventArgs e)
            {
                TemplateInfo.Touch();
            };

            template2s.CellClick += delegate (object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex < 0)
                    return;
                DataGridViewRow r = template2s.Rows[e.RowIndex];
                if (e.ColumnIndex < 0)//row's header
                    return;

                if (r.IsNewRow)//hacky forcing commit a newly added row and display the blank row
                {
                    try
                    {
                        int i = template2s.Rows.Add();
                        r = template2s.Rows[i];
                        Template2 t = TemplateInfo.CreateInitialTemplate();
                        r.Tag = t;
                        r.Cells["Active"].Value = t.Active;
                        r.Cells["Group"].Value = t.Group;
                        r.Cells["OrderWeight"].Value = t.OrderWeight;
                        r.Cells["FileFilterRegex"].Value = t.FileFilterRegex;
                        r.Selected = true;
                    }
                    catch { }
                }

                switch (template2s.Columns[e.ColumnIndex].Name)
                {
                    case "Edit":
                        editTemplate(r);
                        break;
                    case "Copy":
                        Template2T t = (Template2T)r.Tag;
                        if (t == null)
                            return;
                        Template2T t2 = t.Clone<Template2T>();
                        t2.Name = "";
                        //t2.Template.Editor.TestFile = null;
                        LocalInfo.SetLastTestFile(t2, LocalInfo.GetInfo(t).LastTestFile);
                        int i = template2s.Rows.Add(new DataGridViewRow());
                        DataGridViewRow r2 = template2s.Rows[i];
                        r2.Tag = t2;
                        r2.Cells["Name_"].Value = t2.Name.Trim();
                        r2.Cells["Active"].Value = t2.Active;
                        r2.Cells["Group"].Value = t2.Group;
                        r2.Cells["OrderWeight"].Value = t2.OrderWeight;
                        editTemplate(r2);
                        break;
                    case "Edit2":
                        edit2Template(r);
                        break;
                    case "Debug":
                        debugTemplate(r);
                        break;
                }
            };

            progress.Maximum = 10000;

            Load += delegate
            {
                //Load2Gui();
            };
        }

        virtual protected void edit2Template(DataGridViewRow r)
        {
            Template2T t2 = (Template2T)r.Tag;
            if (t2 == null)
                return;

            Template2Form<Template2T> tf = FormManager.Get<Template2Form<Template2T>>(r);
            if (tf != null)
            {
                tf.Activate();
                return;
            }
            tf = NewTemplate2Form(t2);
            FormManager.Set(r, tf);
            tf.FormClosed += delegate
            {
                if (tf.DialogResult != DialogResult.OK)
                    return;
                t2 = tf.Template2;
                r.Tag = t2;
                r.Cells["Active"].Value = t2.Active;
                r.Cells["Group"].Value = t2.Group;
                r.Cells["Comment"].Value = t2.Comment;
                r.Cells["OrderWeight"].Value = t2.OrderWeight;

                TemplateInfo.Touch();
                //setButtonColor(r);
            };
            tf.Show();
        }

        virtual protected void debugTemplate(DataGridViewRow r)
        {
            Template2T t2 = (Template2T)r.Tag;
            if (t2 == null)
                return;

            DebugForm<Template2T> f = FormManager.Get<DebugForm<Template2T>>(r);
            if (f != null)
            {
                f.Activate();
                return;
            }
            f = NewDebugForm();
            FormManager.Set(r, f);
            f.Show();
            f.Template2 = t2;
        }

        //void setButtonColor(DataGridViewRow r)
        //{
        //    Template2 t = (Template2)r.Tag;
        //    if (t == null)
        //        return;
        //    if (!string.IsNullOrWhiteSpace(t.DocumentParserClass))
        //        r.Cells["Edit2"].Style.BackColor = Color.LightCyan;
        //    else if (!string.IsNullOrWhiteSpace(Compiler.GetOnlyCode(t.DocumentParserClassDefinition)))
        //        r.Cells["Edit2"].Style.BackColor = Color.LightYellow;
        //    else
        //        r.Cells["Edit2"].Style.BackColor = SystemColors.ButtonFace;
        //    //r.Cells["Edit2"].Style.ForeColor = Color.Red;
        //}

        virtual public void EditTemplate(string templateName)
        {
            DataGridViewRow row = null;
            foreach (DataGridViewRow r in template2s.Rows)
            {
                if (r.Tag == null)
                    continue;
                Template2 t2 = r.Tag as Template2;
                if (t2.Name == templateName)
                {
                    row = r;
                    break;
                }
            }
            if (row != null)
                editTemplate(row);
        }

        virtual protected void editTemplate(DataGridViewRow r)
        {
            TemplateForm tf = FormManager.Get<TemplateForm>(r);
            if (tf != null)
            {
                tf.Activate();
                return;
            }

            Template2T t = (Template2T)r.Tag;
            if (t == null)
            {
                t = TemplateInfo.CreateInitialTemplate();
                if (!string.IsNullOrWhiteSpace((string)r.Cells["Name_"].Value))
                    t.Name = (string)r.Cells["Name_"].Value;
                r.Tag = t;
            }
            else
            {//synchronize the template with the current format
                Template2T t0 = TemplateInfo.CreateInitialTemplate();
                t.Rectify(t0);
            }

            string lastTestFile = LocalInfo.GetInfo(t).LastTestFile;
            string testFileDefaultFolder = string.IsNullOrWhiteSpace(lastTestFile) ? TemplateTestFileDefaultFolder : PathRoutines.GetFileDir(lastTestFile);
            if (string.IsNullOrWhiteSpace(t.Name))//a copy
                lastTestFile = null;
            TemplateManager tm = new TemplateManager(
                r,
                t.Template,
                lastTestFile,
                testFileDefaultFolder,
                this
            );

            tf = new TemplateForm(tm);
            FormManager.Set(r, tf);
            tf.FormClosed += delegate
            {
                if (tm.LastTestFile != null)
                {
                    LocalInfo.SetLastTestFile(t, tm.LastTestFile);
                    LocalInfo.Save();
                }
            };
            tf.Show();
        }

        public class TemplateManager : TemplateForm.TemplateManager
        {
            public TemplateManager(DataGridViewRow row, Template template, string lastTestFile, string testFileDefaultFolder, TemplateListControl<Template2T> templateListControl) : base(template, lastTestFile, testFileDefaultFolder)
            {
                Row = row;
                this.templateListControl = templateListControl;
            }
            TemplateListControl<Template2T> templateListControl;
            internal DataGridViewRow Row;

            bool firstSave = true;
            override public void Save()
            {
                Template2T t = (Template2T)Row.Tag;
                if (firstSave && templateListControl.TemplateInfo.Template2s.Where(a => a != t && a.Name == Template. Name).FirstOrDefault() != null)
                    throw new Exception("Template '" + Template.Name + "' already exists.");
                firstSave = false;

                Template2T it = templateListControl.TemplateInfo.CreateInitialTemplate();
                foreach (Template.Condition c in it.Template.Conditions)
                    if (Template.Conditions.FirstOrDefault(x => x.Name == c.Name) == null)
                        throw new Exception("The template does not have obligatory condition '" + c.Name + "'.");

                foreach (Template.Field f in it.Template.Fields)
                    if (Template.Fields.FirstOrDefault(x => x.Name == f.Name) == null)
                        throw new Exception("The template does not have obligatory field '" + f.Name + "'.");

                t.Template = Template;
                t.ModifiedTime = DateTime.Now;

                if (LastTestFile != null)
                    templateListControl.LocalInfo.SetLastTestFile(t, LastTestFile);

                if (!templateListControl.TemplateInfo.Template2s.Contains(t))
                    templateListControl.TemplateInfo.Template2s.Add(t);

                templateListControl.TemplateInfo.Touch();

                Row.Cells["Name_"].Value = t.Name;
                Row.Cells["ModifiedTime"].Value = t.GetModifiedTimeAsString();

                Template2Form<Template2T> tf = FormManager.Get<Template2Form<Template2T>>(Row);
                if (tf != null)
                    tf.Template2 = t.CreateCloneByJson();
            }
        }

        virtual public bool SaveFromGui(bool trueIfDeclined)
        {
            try
            {
                if (SavingFromGui?.Invoke() == false)
                    return trueIfDeclined || false;

                if (!TemplateInfo.IsTouched())
                    return true;

                if (!Message.YesNo("Save the recent changes to templates?", FindForm()))
                    return trueIfDeclined || false;

                template2s.EndEdit();//needed to set checkbox values

                TemplateInfo.Template2s.Clear();
                HashSet<string> templateNames = new HashSet<string>();
                foreach (DataGridViewRow r in template2s.Rows)
                {
                    Template2T t = (Template2T)r.Tag;
                    if (t == null)
                        continue;

                    if (templateNames.Contains(t.Name))
                        throw new Exception("Template name '" + t.Name + "' is duplicated!");
                    TemplateInfo.Template2s.Add(t);
                    templateNames.Add(t.Name);
                }
                TemplateInfo.Save();

                return true;
            }
            catch (Exception e)
            {
                Log.Error(e);
                Message.Error(e, FindForm());
                return false;
            }
        }

        virtual public void Load2Gui()
        {
            this.BeginInvoke(() =>
            {
                try
                {
                    Loading2Gui?.Invoke();

                    template2s.Rows.Clear();
                    foreach (Template2T t in TemplateInfo.Template2s)
                    {
                        if (string.IsNullOrWhiteSpace(t.Name))
                            continue;
                        int i = template2s.Rows.Add(new DataGridViewRow());
                        DataGridViewRow r = template2s.Rows[i];
                        r.Cells["Name_"].Value = t.Name.Trim();
                        r.Cells["Active"].Value = t.Active;
                        r.Cells["Group"].Value = t.Group;
                        r.Cells["ModifiedTime"].Value = t.GetModifiedTimeAsString();
                        r.Cells["Comment"].Value = t.Comment;
                        r.Cells["OrderWeight"].Value = t.OrderWeight;
                        r.Cells["UsedTime"].Value = LocalInfo.GetInfo(t).GetUsedTimeAsString();
                        r.Tag = t;
                        //setButtonColor(r);
                    }
                    //templates.Columns["Name_"].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                    //templates.Columns["Name_"].AutoSizeMode = DataGridViewAutoSizeColumnMode.NotSet;

                    saveTemplates.Enabled = TemplateInfo.IsTouched();
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                    Message.Error(ex, FindForm());
                }
            });
        }
    }
}
