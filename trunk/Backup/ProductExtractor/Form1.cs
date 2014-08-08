using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Web;

namespace ProductExtractor
{
    public partial class Form1 : Form
    {
        Extractor ext;
        public Form1()
        {
            InitializeComponent();
        }

        private string getHTML(string url)
        {
            WebClient wc = new WebClient();
            byte[] utf8 = wc.DownloadData(url);
            return Encoding.UTF8.GetString(utf8);
        }

        private void bExtract_Click(object sender, EventArgs e)
        {
            //string url = txtUrl.Text;
            //string html = getHTML(url);

            string url = wbrPage.Url.ToString();
            string html = wbrPage.DocumentText;

            List<stProcduct> ret = ext.extractProduct(url, html);

            //string tmp = "<table width=\"100%\" rules=\"all\" border=\"1\">";
            StringBuilder tmp = new StringBuilder("<table width=\"100%\" rules=\"all\">");
            int i = 0;

            foreach (stProcduct product in ret)
            {
                i++;

                tmp.Append("<tr>");

                tmp.Append("<td width=\"5%\">");
                tmp.Append(i.ToString());
                tmp.Append("</td>");

                tmp.Append("<td width=\"10%\">");
                tmp.AppendFormat("<img src=\"{0}\">", product.image);
                tmp.Append("</td>");

                tmp.Append("<td style=\"text-align:left\">");

                tmp.AppendFormat("<span style=\"color: rgb(255, 0, 0);\">Tên sản phẩm:&nbsp;</span>{0}<br>",
                    product.title);

                tmp.AppendFormat("<span style=\"color: rgb(255, 0, 0);\">Giá:&nbsp;</span>{0}<br>", product.price);

                tmp.AppendFormat("<span style=\"color: rgb(255, 0, 0);\">Thông tin về sản phẩm:</span><br>{0}", product.info);

                tmp.Append("</td>");

                tmp.Append("</tr>");
            }

            tmp.Append("</table>");
            wbrResult.DocumentText = tmp.ToString();
        }

        private void txtUrl_MouseDown(object sender, MouseEventArgs e)
        {
            txtUrl.SelectAll();
        }

        private void bFetch_Click(object sender, EventArgs e)
        {
            Cursor.Current = Cursors.WaitCursor;
            wbrPage.Navigate(txtUrl.Text);
            bExtract.Enabled = true;
            Cursor.Current = Cursors.Default;
         }

        private void traverseTree(stNode p, int spaces)
        {
            if (p == null)
                return;

            string tmp = "";
            for (int i = 0; i != spaces; i++)
                tmp += "  ";
            tmp += "<" + p.tag_name + ">";

            string[] row = new string[]{p.num_node.ToString(), p.height.ToString(), p.simhash.ToString(), tmp};
            dataGridView1.Rows.Add(row);

            traverseTree(p.child, spaces + 1);
            traverseTree(p.sibling, spaces);
        }
        private void wbrPage_Click(object sender, HtmlElementEventArgs e)
        {
            //HtmlElement ele = wbrPage.Document.GetElementFromPoint(e.ClientMousePosition);
            
        }

        private void wbrPage_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            wbrPage.Document.MouseUp += new HtmlElementEventHandler(wbrPage_Click);

            string url = wbrPage.Url.AbsoluteUri;
            string html = wbrPage.DocumentText;

            ext = new Extractor();
            ext.parseHtml(ref html);

            stNode p = ext.root;
            traverseTree(p, 0);
        }
    }
}