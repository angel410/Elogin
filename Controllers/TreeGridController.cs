using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using eLogin.Data;
using eLogin.Models;
using Microsoft.AspNetCore.Authorization;
using eLogin.Models.Roles;
using Syncfusion.EJ2.Base;
using System.Collections;
using Syncfusion.EJ2.Linq;

namespace eLogin.Controllers
{
    [Authorize(Roles = nameof(eLoginAdmin))]
    public class TreeGridController : Controller
    {
        private readonly DatabaseContext _context;

        public TreeGridController(DatabaseContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            //var data = TreeGridItems.GetSelfData();
            //ViewBag.DataSource = data;
            return View();
        }
        public IActionResult DataSource([FromBody] DataManagerRequest dm)
        {
            IEnumerable DataSource = TreeData.GetSelfData();
            DataOperations operation = new DataOperations();
            if (dm.Where != null && dm.Where.Count > 0) //filtering
            {
                DataSource = operation.PerformFiltering(DataSource, dm.Where, "and");
            }
            if (dm.Search != null && dm.Search.Count > 0)
            {
                DataSource = operation.PerformSearching(DataSource, dm.Search);
            }
            if (dm.Sorted != null && dm.Sorted.Count > 0)
            {
                DataSource = operation.PerformSorting(DataSource, dm.Sorted);
            }
            var count = TreeData.GetSelfData().Count();
            if (dm.Skip != 0)
            {
                DataSource = operation.PerformSkip(DataSource, dm.Skip);   //Paging
            }
            if (dm.Take != 0)
            {
                DataSource = operation.PerformTake(DataSource, dm.Take);
            }

            JsonResult x = dm.RequiresCounts ? Json(new { result = DataSource, count = count }) : Json(DataSource);

            return x;

        }
        //update operation
        public ActionResult Update([FromBody] CRUDModel value)
        {
            var val = TreeData.GetSelfData().Where(ds => ds.TaskId == value.Value.TaskId).FirstOrDefault();
            val.TaskName = value.Value.TaskName;
            val.Duration = value.Value.Duration;
            return Json(val);
        }
        //Insert operation
        public ActionResult Insert([FromBody] CRUDModel value, int rowIndex)
        {
            var i = 0;
            if (value.Action == "insert")
            {
                rowIndex = value.RelationalKey; //setting rowIndex for inserting in that particular index
            }
            for (; i < TreeData.GetSelfData().Count; i++)
            {
                if (TreeData.GetSelfData()[i].TaskId == rowIndex)
                {
                    if (value.Action == "insert")
                    {
                        value.Value.ParentId = rowIndex;
                    }
                    if (TreeData.GetSelfData()[i].isParent == null)
                    {
                        TreeData.GetSelfData()[i].isParent = true;
                    }
                    break;

                }
            }
            i += FindChildRecords(rowIndex); // finding child records of selected record so that we can insert record next to those childs
            TreeData.GetSelfData().Insert(i + 1, value.Value); //inserting the new data

            return Json(value.Value);
        }
        public int FindChildRecords(int? id)
        {
            var count = 0;
            for (var i = 0; i < TreeData.GetSelfData().Count; i++)
            {
                if (TreeData.GetSelfData()[i].ParentId == id)
                {
                    count++;
                    count += FindChildRecords(TreeData.GetSelfData()[i].TaskId);
                }
            }
            return count;
        }
        //Delete operation
        public Object Remove([FromBody] CRUDModel value)
        {
            TreeData.GetSelfData().Remove(TreeData.GetSelfData().Where(or => or.TaskId.Equals(value.Key)).FirstOrDefault());
            return Json(value);
        }
        public object Delete([FromBody] CRUDModel value)
        {
            if (value.Deleted != null)
            {
                for (var i = 0; i < value.Deleted.Count; i++)
                {
                    TreeData.GetSelfData().Remove(TreeData.GetSelfData().Where(ds => ds.TaskId == value.Deleted[i].TaskId).FirstOrDefault());
                }
            }
            return new { deleted = value.Deleted };

        }
        //row Drag and Drop handler
        public bool MyTestMethod([FromBody] CRUDModel value)
        {
            if (value.pos.position == "bottomSegment" || value.pos.position == "topSegment")
            {
                //for bottom and top segment drop position. If the dragged record is the only child for a particular record,
                //we need to set parentItem of dragged record to null and isParent of dragged record's parent to false 
                if (value.Value.ParentId != null) // if dragged record has parent
                {
                    var childCount = 0;
                    int parent1 = (int)value.Value.ParentId;
                    childCount += FindChildRecords(parent1); // finding the number of child for dragged record's parent
                    if (childCount == 1) // if the dragged record is the only child for a particular record
                    {
                        var i = 0;
                        for (; i < TreeData.GetSelfData().Count; i++)
                        {
                            if (TreeData.GetSelfData()[i].TaskId == parent1)
                            {
                                //set isParent of dragged record's parent to false 
                                TreeData.GetSelfData()[i].isParent = false;
                                break;
                            }
                            if (TreeData.GetSelfData()[i].TaskId == value.Value.TaskId)
                            {
                                //set parentItem of dragged record to null
                                TreeData.GetSelfData()[i].ParentId = null;
                                break;
                            }


                        }
                    }
                }
                TreeData.GetSelfData().Remove(TreeData.GetSelfData().Where(ds => ds.TaskId == value.pos.dragidMapping).FirstOrDefault());
                var j = 0;
                for (; j < TreeData.GetSelfData().Count; j++)
                {
                    if (TreeData.GetSelfData()[j].TaskId == value.pos.dropidMapping)
                    {
                        //set drgged records parentItem with parentItem of
                        //record in dropindex
                        value.Value.ParentId = TreeData.GetSelfData()[j].ParentId;
                        break;
                    }
                }
                if (value.pos.position == "bottomSegment")
                {
                    this.Insert(value, value.pos.dropidMapping);
                }
                else if (value.pos.position == "topSegment")
                {
                    //TreeGridItems.GetSelfData().Remove(TreeGridItems.GetSelfData().Where(ds => ds.TaskId == pos.dragidMapping).FirstOrDefault());
                    this.InsertAtTop(value, value.pos.dropidMapping);
                }
            }
            else if (value.pos.position == "middleSegment")
            {
                TreeData.GetSelfData().Remove(TreeData.GetSelfData().Where(ds => ds.TaskId == value.pos.dragidMapping).FirstOrDefault());
                value.Value.ParentId = value.pos.dropidMapping;
                FindDropdata(value.pos.dropidMapping);
                this.Insert(value, value.pos.dropidMapping);
            }
            return true;
        }
        public void InsertAtTop([FromBody] CRUDModel value, int rowIndex)
        {
            var i = 0;
            for (; i < TreeData.GetSelfData().Count; i++)
            {
                if (TreeData.GetSelfData()[i].TaskId == rowIndex)
                {
                    break;

                }
            }
            i += FindChildRecords(rowIndex);
            TreeData.GetSelfData().Insert(i - 1, value.Value);
        }

        public void FindDropdata(int key)
        {
            var i = 0;
            for (; i < TreeData.GetSelfData().Count; i++)
            {
                if (TreeData.GetSelfData()[i].TaskId == key)
                {
                    TreeData.GetSelfData()[i].isParent = true;
                }
            }
        }
        public class CRUDModel
        {

            public TreeData Value;
            public int Key { get; set; }

            public int RelationalKey { get; set; }

            public List<TreeData> Deleted { get; set; }
            public TreeGridData pos;
            public string Action { get; set; }
        }
        public class TreeGridData
        {
            public int dragidMapping { get; set; }
            public int dropidMapping { get; set; }
            public string position { get; set; }
        }



    }
}
