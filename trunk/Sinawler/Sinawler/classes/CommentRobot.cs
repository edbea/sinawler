using System;
using System.Collections.Generic;
using System.Text;
using Sina.Api;
using System.Threading;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;
using Sinawler.Model;
using System.Data;

namespace Sinawler
{
    class CommentRobot : RobotBase
    {
        private long lCommentUserID = 0;    //评论人的UserID

        public long CurrentUserID
        { get { return lCommentUserID; } }

        //构造函数，需要传入相应的新浪微博API
        public CommentRobot(SinaApiService oAPI) : base(oAPI)
        {
            queueBuffer = new QueueBuffer(QueueBufferTarget.FOR_COMMENT);
            strLogFile = Application.StartupPath + "\\" + DateTime.Now.Year.ToString() + DateTime.Now.Month.ToString() + DateTime.Now.Day.ToString() + DateTime.Now.Hour.ToString() + DateTime.Now.Minute.ToString() + DateTime.Now.Second.ToString() + "_comment.log";
        }

        /// <summary>
        /// 开始爬行取微博评论
        /// </summary>
        public void Start()
        {
            //不加载用户队列，完全依靠UserRobot传递过来
            while (lstWaitingID.Count == 0)
            {
                if (blnAsyncCancelled) return;
                Thread.Sleep(50);   //若队列为空，则等待
            }
            long lStartSID = lstWaitingID.First.Value;
            long lCurrentSID = 0;
            long lHead = 0;
            //对队列无限循环爬行，直至有操作暂停或停止
            while (true)
            {
                if (blnAsyncCancelled) return;
                while (blnSuspending)
                {
                    if (blnAsyncCancelled) return;
                    Thread.Sleep(50);
                }

                //将队头取出
                lCurrentSID = lstWaitingID.First.Value;
                //从数据库队列缓存中移入元素
                lHead = queueBuffer.FirstValue;
                if (lHead > 0)
                {
                    lstWaitingID.AddLast( lHead );
                    queueBuffer.Remove( lHead );
                }
                //移入队尾，并从队头移除
                if (lstWaitingID.Count <= iQueueLength)
                    lstWaitingID.AddLast( lCurrentSID );
                else
                    queueBuffer.Enqueue( lCurrentSID );
                lstWaitingID.RemoveFirst();

                #region 预处理
                if (lCurrentSID == lStartSID)  //说明经过一次循环迭代
                {
                    if (blnAsyncCancelled) return;
                    while (blnSuspending)
                    {
                        if (blnAsyncCancelled) return;
                        Thread.Sleep(50);
                    }

                    //日志
                    Log("开始爬行之前增加迭代次数...");
                    Comment.NewIterate();
                }
                #endregion
                #region 微博相应评论
                if (blnAsyncCancelled) return;
                while (blnSuspending)
                {
                    if (blnAsyncCancelled) return;
                    Thread.Sleep(50);
                }

                //日志
                Log("爬取微博" + lCurrentSID.ToString() + "的评论...");
                //爬取当前微博的评论
                List<Comment> lstComment = crawler.GetCommentsOf(lCurrentSID);
                //日志
                Log("爬得微博"+lCurrentSID.ToString()+"的" + lstComment.Count.ToString() + "条评论。");

                foreach (Comment comment in lstComment)
                {
                    if (blnAsyncCancelled) return;
                    while (blnSuspending)
                    {
                        if (blnAsyncCancelled) return;
                        Thread.Sleep(50);
                    }
                    lCurrentID = comment.comment_id;
                    lCommentUserID = comment.user_id;
                    if (!Comment.Exists(lCurrentID))
                    {
                        //日志
                        Log("将评论" + lCurrentID.ToString() + "存入数据库...");
                        comment.Add();
                    }
                }
                #endregion
                //最后再将刚刚爬行完的StatusID加入队尾
                //日志
                Log("微博" + lCurrentSID.ToString() + "的评论已爬取完毕。");
                //日志
                Log("调整请求间隔为" + crawler.SleepTime.ToString() + "毫秒。本小时剩余" + crawler.ResetTimeInSeconds.ToString() + "秒，剩余请求次数为" + crawler.RemainingHits.ToString() + "次");
            }
        }

        public override void Initialize()
        {
            //初始化相应变量
            blnAsyncCancelled = false;
            blnSuspending = false;
            if (lstWaitingID != null) lstWaitingID.Clear();

            //清空数据库队列缓存
            queueBuffer.Clear();
        }
    }
}
