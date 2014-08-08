using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;
using System.Web;
using System.Globalization;

namespace ProductExtractor
{
    class stNode
    {
        public string tag_name;
        public stNode child;
        public stNode sibling;
        public int num_node;
        public int height;
        public int start_text;
        public int end_text;
        public bool is_checked;
        public uint simhash;

        public stNode(string name)
        {
            tag_name = name;
            child = null;
            sibling = null;
            simhash = 0;
            height = 0;
            is_checked = false;
        }
    }
    class stResult
    {
        public int start_text;
        public int end_text;
        //public Dictionary<string, int> words = new Dictionary<string,int>();

        public stResult(int start, int end)
        {
            start_text = start;
            end_text = end;
        }
    }
    class stProcduct
    {
        public string title;
        public string price;
        public string image;
        public string info;
    }

    class Extractor
    {
        static string[] ignore_tags = { "br", "img", "area", "col", "hr", "input", "link", "param", "font", "i",
            "u", "b", "h1", "h2", "h3", "h4", "h5", "h6"};

        static string[] money_units = { "VND", "VNĐ", "USD", "$", "vnd", "vnđ", "usd" };

        //static string seperators = " \t\r\n<>.,?;!-_+'\"(){}[]\u00a0|";

        public stNode root = null;

        bool is_ignore_tag(string tag)
        {
            for (int i = 0; i != ignore_tags.Length; i++)
                if (tag == ignore_tags[i])
                    return true;
            return false;
        }
        void calcNumNode(stNode node)
        {
            stNode p = node.child;
            while (p != null)
            {
                calcNumNode(p);
                node.num_node += p.num_node;
                if (node.height < p.height)
                    node.height = p.height;

                p = p.sibling;
            }

            node.num_node += 1;
            node.height += 1;
        }
        void calcSimHash(stNode node)
        {
            int[] bitarray = new int[32];
            stNode p = node.child;
            while (p != null)
            {
                calcSimHash(p);
                uint tmp = p.simhash;
                for (int i = 0; i != 32; i++)
                {
                    if (tmp % 2 == 1)
                        bitarray[i] += p.num_node;
                    else
                        bitarray[i] -= p.num_node;
                    tmp /= 2;
                }
                p = p.sibling;
            }
            uint tmp1 = 0;
            for (int i = 0; i != 32; i++)
            {
                tmp1 *= 2;
                if (bitarray[i] > 0)
                    tmp1 += 1;
            }
            MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider();
            byte[] data = Encoding.ASCII.GetBytes(node.tag_name);
            data = md5.ComputeHash(data);
            node.simhash = BitConverter.ToUInt32(data, 0) ^ tmp1;
        }
        bool getTagName(string html, int pos, ref string tagname)
        {
            bool is_closed_tag = false;
            pos++;
            if (html[pos] == '/')
            {
                is_closed_tag = true;
                pos++;
            }

            int pos1 = pos;
            while (html[pos1] != ' ' && html[pos1] != '>' && pos1 < html.Length)
                pos1++;

            tagname = html.Substring(pos, pos1 - pos).ToLower();
            return is_closed_tag;
        }
        stNode parse(string html, string tagname, ref int pos)
        {
            stNode node = new stNode(tagname);
            node.start_text = pos;
            stNode p = node;
            bool check = true;

            while (pos != -1)
            {
                pos = html.IndexOf('<', pos + 1);
                if (pos == -1)
                    return node;

                string tag_name = "";
                int start_tag = pos;
                bool closed_tag = getTagName(html, pos, ref tag_name);
                if (is_ignore_tag(tag_name))
                {
                    continue;
                }
                if (!closed_tag)
                {
                    if (check)
                    {
                        p.child = parse(html, tag_name, ref pos);
                        if (pos != -1)
                            p.end_text = pos;
                        else
                            p.end_text = html.Length;
                        check = false;
                    }
                    else
                    {
                        p.sibling = new stNode(tag_name);
                        p = p.sibling;
                        p.start_text = start_tag;
                        check = true;
                    }
                }
                else
                {
                    pos = html.IndexOf('>', pos + 1);
                    if (!check)
                    {
                        return node;
                    }
                    else
                    {
                        check = false;
                        p.end_text = pos;
                    }
                }
            }

            return node;
        }
        public void parseHtml(ref string html)
        {
            removeSpecificTag(ref html, "script");
            removeSpecificTag(ref html, "style");
            html = removeComments(html);
            int pos = html.IndexOf('<');

            string tagname = "";
            while (pos != -1)
            {
                getTagName(html, pos, ref tagname);
                if (tagname != "html")
                    pos = html.IndexOf('<', pos + 1);
                else
                    break;
            }
           
            root = parse(html, tagname, ref pos);
            calcNumNode(root);
            calcSimHash(root);

            //return root;
        }
        int compareSimhash(uint sh1, uint sh2)
        {
            uint tmp = sh1 ^ sh2;
            int diff_count = 0;
            while (tmp != 0)
            {
                if (tmp % 2 == 1)
                    diff_count++;
                tmp /= 2;
            }

            return diff_count;
        }

        string removeComments(string html)
        {
            int pos = html.IndexOf("<!DOCTYPE");
            if(pos == -1)
                pos = html.IndexOf("<!doctype");
            int pos1 = 0;
            if (pos != -1)
            {
                pos1 = html.IndexOf(">", pos + 1);
                html = html.Remove(pos, pos1 - pos + 1);
            }

            pos = html.IndexOf("<!", 0);
            while (pos != -1)
            {
                pos1 = html.IndexOf("-->", pos);
                //html = html.Substring(0, pos) + html.Substring(pos1 + 1);
                html = html.Remove(pos, pos1 - pos + 3);
                pos = html.IndexOf("<!", pos);
            }

            return html;
        }
        void removeSpecificTag(ref string html, string tagname)
        {
            int pos = html.IndexOf('<');
            while (pos != -1 && html.Length - pos > tagname.Length)
            {
                if (html.Substring(pos + 1, tagname.Length).ToLower() == tagname)
                {
                    int pos1 = html.IndexOf("</", pos);
                    while(pos1 != -1 && html.Length - pos1 > tagname.Length + 1)
                    {
                        if(html.Substring(pos1+2, tagname.Length).ToLower() == tagname)
                        {
                            pos1 = html.IndexOf(">", pos1);
                            html = html.Remove(pos, pos1 - pos + 1);
                            pos = html.IndexOf('<', pos);
                            break;
                        }
                        else
                            pos1 = html.IndexOf("</", pos1 + 1);
                    }
                }
                else
                    pos = html.IndexOf('<', pos + 1);
            }
        }

        string removeTag(string s)
        {
            string ret = "";
            int pos = s.IndexOf('<');
            int pos1 = 0;
            while (pos != -1)
            {
                ret += s.Substring(pos1, pos - pos1);
                pos1 = s.IndexOf('>', pos);
                if (pos1 == -1 || pos1 == s.Length - 1)
                    return ret;

                pos1++;
                pos = s.IndexOf('<', pos1);
            }

            ret += s.Substring(pos1);

            return ret;
        }
        bool hasAttribute(string s, int pos, string attr)
        {
            int pos1 = s.IndexOf('>', pos);
            if (pos1 == -1)
                return false;

            string tmp = s.Substring(pos, pos1 - pos).ToLower();
            //pos1 = tmp.IndexOf("href");
            pos1 = tmp.IndexOf(attr);

            if (pos1 == -1)
                return false;

            return true;
        }
        bool isInLink(string s, int pos)
        {
            int pos1 = s.IndexOf("<");
            while (pos1 != -1)
            {
                if (char.ToLower(s[pos1 + 1]) == 'a')
                {
                    int pos2 = s.IndexOf("</", pos1);
                    while (pos2 != -1)
                    {
                        if (char.ToLower(s[pos2 + 2]) == 'a')
                        {
                            if (pos1 < pos && pos < pos2)
                                return true;
                            else
                            {
                                pos1 = pos2;
                                break;
                            }
                        }
                        else
                            pos2 = s.IndexOf("</", pos2 + 1);
                    }
                }
                
                pos1 = s.IndexOf("<", pos1 + 1);
            }

            return false;
        }
        string getTitle(ref string s)
        {
            int pos = s.IndexOf('<');

            while (pos != -1 && pos != s.Length - 1)
            {
                int pos1 = s.IndexOf('>', pos + 1);
                int pos2 = s.IndexOf('<', pos + 1);

                while (pos2 != -1 && pos1 != -1 && pos2 < pos1)
                {
                    pos1 = s.IndexOf('>', pos1 + 1);
                    pos2 = s.IndexOf('<', pos2 + 1);
                }

                if (pos1 == -1)
                    return "";

                if ((s[pos + 1] == 'a' || s[pos + 1] == 'A') && hasAttribute(s, pos, "href"))
                {
                    while (pos1 != -1)
                    {
                        if (s[pos1 + 1] == '/' && (s[pos1 + 2] == 'a' || s[pos1 + 2] == 'A'))
                            break;

                        pos1 = s.IndexOf("</", pos1 + 1);
                    }

                    if (pos1 != -1)
                    {
                        pos1 += 3;
                        string tmp = removeTag(s.Substring(pos, pos1 - pos + 1));
                        tmp = System.Web.HttpUtility.HtmlDecode(tmp);
                        tmp = tmp.Trim("\u00a0 \t\r\n".ToCharArray());
                        if (tmp.Length > 0)
                        {
                            s = s.Remove(pos, pos1 - pos + 1);
                            return tmp;
                        }
                    }
                }

                pos = pos2;
            }
            return "";
        }

        string getNearestText(ref string s, int pos)
        {
            int pos1 = s.IndexOf(">", pos + 1);
            while (pos1 != -1)
            {
                int pos2 = s.IndexOf("<", pos1);
                if (pos2 == -1)
                    return "";

                string tmp = s.Substring(pos1 + 1, pos2 - pos1 - 1);
                tmp = tmp.Trim("\u00a0 \t\r\n".ToCharArray());
                if (tmp.Length > 0)
                {
                    s = s.Remove(pos1 + 1, pos2 - pos1 - 1);
                    return tmp;
                }

                pos1 = s.IndexOf(">", pos2);
            }

            return "";
        }
        string getImg(string url, string s, ref bool isImgLink, ref int imgPos)
        {
            int pos = s.IndexOf('<');

            while (pos != -1 && pos != s.Length - 1)
            {
                int pos1 = s.IndexOf('>', pos + 1);
                int pos2 = s.IndexOf('<', pos + 1);

                while (pos2 != -1 && pos1 != -1 && pos2 < pos1)
                {
                    pos1 = s.IndexOf('>', pos1 + 1);
                    pos2 = s.IndexOf('<', pos2 + 1);
                }

                if (pos1 == -1)
                    return "";

                if (s.Substring(pos+1, 3).ToLower() == "img")
                {
                    string tmp = s.Substring(pos, pos1 - pos + 1);
                    int pos3 = tmp.ToLower().IndexOf("src");
                    if(pos3 != -1)
                    {
                        imgPos = pos;
                        isImgLink = isInLink(s, imgPos);
                        pos3 += 3;
                        while (tmp[pos3] == ' ' || tmp[pos3] == '=')
                            pos3++;

                        char quote = tmp[pos3];
                        if(quote == '\'' || quote == '"')
                        {
                            pos3++;
                            int pos4 = tmp.IndexOf(quote, pos3);
                            string relativeUrl = tmp.Substring(pos3, pos4 - pos3);
                            Uri u = new Uri(new Uri(url), relativeUrl);
                            return u.AbsoluteUri;
                        }
                        else
                        {
                            int pos4 = pos3;
                            while (tmp[pos4] != ' ' && tmp[pos4] != '>')
                                pos4++;
                            string relativeUrl = tmp.Substring(pos3, pos4 - pos3);
                            Uri u = new Uri(new Uri(url), relativeUrl);
                            return u.AbsoluteUri;
                        }
                    }
                }
                pos = pos2;
            }

            return "";
        }
        string getPrice(ref string s)
        {
            s = s.Replace("\n", "");
            s = s.Replace("<br>", "\n");
            s = s.Replace("<br/>", "\n");
            s = s.Replace("<br />", "\n");
            s = s.Replace("</div>", "\n");
            s = s.Replace("</DIV>", "\n");
            s = removeTag(s);
            s = HttpUtility.HtmlDecode(s);

            foreach (string unit in money_units)
            {
                int pos = s.IndexOf(unit);
                if (pos != -1)
                {
                    int pos1 = pos + unit.Length;

                    while (pos >= 0 && !char.IsDigit(s[pos]))
                        pos--;

                    if(pos < 0)
                        continue;

                    char c = s[pos];
                    while (pos > 0 && (c == ',' || c == '.' || c == ' ' || char.IsDigit(c)))
                        c = s[--pos];

//                     string ret = s.Substring(pos + 1, pos1 - pos - 1);
//                     pos = s.LastIndexOf("\n", pos);
//                     if (pos == -1)
//                         pos = 0;
//                     pos1 = s.IndexOf("\n", pos + 1);
//                     if (pos1 == -1)
//                         s = s.Remove(pos);
//                     else
//                         s = s.Remove(pos, pos1 - pos);
// 
//                     return ret;
                    return s.Substring(pos+1, pos1 - pos - 1);
                }
            }
            return "";
        }

        void findPossNodes(stNode node, ref List<stNode> possibleNodes)
        {
            if (node.height < 3)
                return;

            stNode p = node.child;

            while (p != null)
            {
                stNode q = p.sibling;

                while (q != null)
                {
                    if (compareSimhash(p.simhash, q.simhash) < 8 && p.height > 2 && q.height > 2)
                    {
                        if (p.is_checked == false)
                        {
                            p.is_checked = true;
                            possibleNodes.Add(p);
                        }

                        if (q.is_checked == false)
                        {
                            q.is_checked = true;
                            possibleNodes.Add(q);
                        }
                    }

                    q = q.sibling;
                }

                if (p.is_checked == false)
                    findPossNodes(p, ref possibleNodes);

                p = p.sibling;
            }
        }
        bool findDataRegion(stNode node, ref Queue<stNode> list_node)
        {
            if (node.height < 3)
                return false;

            bool ret = false;
            stNode p = node.child;

            while (p != null)
            {
                p.is_checked = false;
                p = p.sibling;
            }

            p = node.child;
            while (p != null)
            {
                stNode q = p.sibling;

                while (q != null)
                {
                    if (compareSimhash(p.simhash, q.simhash) < 4 && p.height > 2 && q.height > 2)
                    {
                        if (p.is_checked == false)
                        {
                            list_node.Enqueue(p);
                            p.is_checked = true;
                        }

                        if (q.is_checked == false)
                        {
                            list_node.Enqueue(q);
                            q.is_checked = true;
                        }

                        ret = true;
                    }

                    q = q.sibling;
                }

                if (p.is_checked == false)
                    if (findDataRegion(p, ref list_node))
                        ret = true;

                p = p.sibling;
            }

            return ret;
        }

        List<stResult> getRegionList(stNode root)
        {
            List<stNode> possibleNodes = new List<stNode>();
            findPossNodes(root, ref possibleNodes);

            List<stResult> ret = new List<stResult>();
            foreach (stNode node in possibleNodes)
            {
                Queue<stNode> q = new Queue<stNode>();
                q.Enqueue(node);

                while (q.Count > 0)
                {
                    stNode p = q.Dequeue();
                    if (findDataRegion(p, ref q) == false)
                        ret.Add(new stResult(p.start_text, p.end_text));
                }
            }

            return ret;
        }

        int strspn(string html, int pos, string chars)
        {
            int p = pos;
            while (p < html.Length)
            {
                if (chars.IndexOf(html[p]) == -1)
                    break;
                p++;
            }

            return p;
        }

        int strcspn(string html, int pos, string chars)
        {
            int p = pos;
            while (p < html.Length)
            {
                if (chars.IndexOf(html[p]) != -1)
                    break;
                p++;
            }

            return p;
        }

        //void countWords(string html, ref stResult region)
        //{
        //    string text = removeTag(html);

        //    int pos = 0;
        //    int pos1 = pos; 
        //    int end = text.IndexOf("<");
        //    if (end == -1)
        //        end = text.Length;
            
        //    while (true)
        //    {
        //        pos = strspn(text, pos1, seperators);
        //        if (pos == text.Length)
        //            break;

        //        if (pos < end)
        //        {
        //            pos1 = strcspn(text, pos, seperators);

        //            if (pos1 <= end)
        //            {
        //                string word = text.Substring(pos, pos1 - pos).ToLower();
        //                int value;
        //                if (region.words.TryGetValue(word, out value))
        //                    region.words[word] = value + 1;
        //                else
        //                    region.words.Add(word, 1);

        //                continue;
        //            }
        //        }

        //        if (end >= text.Length - 1)
        //            break;

        //        pos1 = text.IndexOf(">", end + 1);
        //        if (pos != -1)
        //            break;

        //        end = text.IndexOf("<", pos);
        //        if (end == -1)
        //            end = text.Length;
        //    }
        //}

        //void countWords(string html, ref List<stResult> regions, ref Dictionary<string,int> totalWords)
        //{
        //    for (int i = 0; i != regions.Count; i++ )
        //    {
        //        stResult region = regions[i];
        //        string text = html.Substring(region.start_text, region.end_text - region.start_text + 1);

        //        countWords(System.Web.HttpUtility.HtmlDecode(text),ref region);

        //        foreach (KeyValuePair<string, int> pair in region.words)
        //        {
        //            int value;
        //            if (totalWords.TryGetValue(pair.Key, out value))
        //                totalWords[pair.Key] = value + 1;
        //            else
        //                totalWords.Add(pair.Key, pair.Value);
        //        }
        //    }
        //}

        public List<stProcduct> extractProduct(string url, string html)
        {
            parseHtml(ref html);
            List<stResult> regions = getRegionList(root);

            List<stProcduct> ret = new List<stProcduct>();

            //Dictionary<string, int> totalWords = new Dictionary<string, int>();
            //countWords(html, ref regions, ref totalWords);

            foreach (stResult region in regions)
            {
                //if(isDataRegion(region, totalWords))
                //{
                    //string text = html.Substring(region.start_text, region.end_text - region.start_text + 1);

                string text = html.Substring(region.start_text, region.end_text - region.start_text + 1);
                if (isDataRegion(text))
                {
                    //string text = html.Substring(region.start_text, region.end_text - region.start_text + 1);
                    stProcduct product = new stProcduct();
                    bool isLinkImg = false;
                    int imgPos = 0;
                    product.image = getImg(url, text, ref isLinkImg, ref imgPos);
                    product.title = getTitle(ref text);
                    if (product.title.Length == 0 && isLinkImg)
                        product.title = getNearestText(ref text, imgPos);
                    product.price = getPrice(ref text);
                    product.info = text.Replace("\n\n", "\n").Trim("\n\t ".ToCharArray()).Replace("\n", "<br>");

                    ret.Add(product);
                }
                
            }

            return ret;
        }

//         bool isDataRegion(stResult region, Dictionary<string,int> totalWords)
//         {
//             int count = 0;
//             foreach (KeyValuePair<string, int> p in region.words)
//             {
//                 if (totalWords[p.Key] > 5)
//                     count++;
//             }
// 
//             return count > 4;
//         }

        bool isDataRegion(string html)
        {
            int n_img = countTag(html, "img");

            html = removeTag(html);
            html = System.Web.HttpUtility.HtmlDecode(html);

            int n_unit = countUnit(html);

            if (n_unit == 0)
            {
                int n_digit = countDigit(html);
                if (n_digit > 29)
                {
                    if (n_img == 0)
                        return false;
                    else
                        return true;
                }

                return false;
            }

            if (n_img == 0)
                return false;

            return true;
        }

        int countItem(string html, string str)
        {
            int count = 0;
            CompareInfo ci = CultureInfo.CurrentCulture.CompareInfo;

            int pos = ci.IndexOf(html, str, CompareOptions.IgnoreCase);
            while (pos != -1)
            {
                count += 1;
                pos = ci.IndexOf(html, str, pos + 1, CompareOptions.IgnoreCase);
            }

            return count;
        }

        int countTag(string html, string tagname)
        {
            return countItem(html, "<" + tagname);
        }

        int countUnit(string html)
        {
            string[] units = { "vnd", "vnđ", "usd", "$", "£" };
            int count = 0;
            foreach (string unit in units)
            {
                count += countItem(html, unit);
            }

            return count;
        }

        int countDigit(string html)
        {
            int count = 0;
            foreach (char c in html)
            {
                if (char.IsDigit(c))
                {
                    count += 1;
                }
            }

            return count;
        }
    }
}
