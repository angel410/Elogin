namespace eLogin.Models
{
    public class TreeData
    {
        public TreeData() { }

        public int TaskId { get; set; }



        public string TaskName { get; set; }
        public int Duration { get; set; }
        public int? ParentId { get; set; }
        public bool? isParent { get; set; }


        //public static List<TreeData> BusinessObjectCollection = new List<TreeData>();



        public static List<TreeData> GetSelfData()
        {
            List<TreeData> BusinessObjectCollection = new List<TreeData>();
            if (BusinessObjectCollection.Count == 0)
            {
                BusinessObjectCollection.Add(new TreeData()
                {
                    TaskId = 1,
                    TaskName = "Parent Task 1",
                    Duration = 10,
                    ParentId = null,
                    isParent = true
                });
                BusinessObjectCollection.Add(new TreeData()
                {
                    TaskId = 2,
                    TaskName = "Child task 1",
                    Duration = 4,
                    ParentId = 1,
                    isParent = null
                });



                BusinessObjectCollection.Add(new TreeData()
                {
                    TaskId = 5,
                    TaskName = "Parent Task 2",
                    Duration = 10,
                    ParentId = null,
                    isParent = true
                });
                BusinessObjectCollection.Add(new TreeData()
                {
                    TaskId = 6,
                    TaskName = "Child task 2",
                    Duration = 4,
                    ParentId = 5,
                    isParent = null
                });



                BusinessObjectCollection.Add(new TreeData()
                {
                    TaskId = 10,
                    TaskName = "Parent Task 3",
                    Duration = 10,
                    ParentId = null,
                    isParent = true
                });
                BusinessObjectCollection.Add(new TreeData()
                {
                    TaskId = 11,
                    TaskName = "Child task 3",
                    Duration = 4,
                    ParentId = 10,
                    isParent = false
                });



            }



            return BusinessObjectCollection;
        }
    }
    //public class States
    //{
    //    public string countryName { get; set; }
    //    public double gdp { get; set; }
    //    public double unemployement { get; set; }
    //    public string timezone { get; set; }
    //    public string coordinates { get; set; }
    //}



    //public class Country
    //{
    //    public Country() { }
    //    public string countryName { get; set; }
    //    public double gdp { get; set; }
    //    public double unemployement { get; set; }
    //    public string timezone { get; set; }
    //    public string coordinates { get; set; }


    //    public List<States> Children { get; set; }

    //    public static List<Country> BusinessObjectCollection = new List<Country>();

    //    public static List<Country> GetTreeData()
    //    {
    //        if (BusinessObjectCollection.Count == 0)
    //        {



    //            Country Record1 = null;

    //            Record1 = new Country()
    //            {
    //                countryName = "USA",
    //                gdp = 2.2,
    //                unemployement = 3.9,
    //                timezone = "UTC -5 to -10",
    //                coordinates = "37.0902° N, 95.7129° W",
    //                Children = new List<States>(),

    //            };
    //            States Child1 = new States()
    //            {
    //                countryName = "Washington, D.C.",
    //                gdp = 4.7,
    //                timezone = "UTC -5",
    //                coordinates = "38.9072° N, 77.0369° W",
    //                unemployement = 4.3
    //            };

    //            States Child2 = new States()
    //            {
    //                countryName = "New York",
    //                gdp = 1.9,
    //                timezone = "UTC -7",
    //                coordinates = "40.7128° N, 74.0060° W",
    //                unemployement = 3.9
    //            };
    //            States Child3 = new States()
    //            {
    //                countryName = "New Mexico",
    //                gdp = 0.1,
    //                timezone = "UTC -9",
    //                coordinates = "34.5199° N, 105.8701° W",
    //                unemployement = 4.7
    //            };
    //            Record1.Children.Add(Child1);
    //            Record1.Children.Add(Child2);
    //            Record1.Children.Add(Child3);
    //            Country Record2 = new Country()
    //            {
    //                countryName = "Greece",
    //                gdp = 1.5,
    //                unemployement = 20.8,
    //                timezone = "UTC +2.0",
    //                coordinates = "39.0742° N, 21.8243° E",
    //                Children = new List<States>(),

    //            };
    //            States Child5 = new States()
    //            {
    //                countryName = "Athens",
    //                gdp = 1,
    //                timezone = "UTC +2.0",
    //                coordinates = "37.9838° N, 23.7275° E",
    //                unemployement = 7.7
    //            };

    //            States Child6 = new States()
    //            {
    //                countryName = "Arcadia",
    //                gdp = 2.5,
    //                timezone = "UTC +2.0",
    //                coordinates = "37.9838° N, 23.7275° E",
    //                unemployement = 3.0
    //            };
    //            States Child7 = new States()
    //            {
    //                countryName = "Argolis",
    //                gdp = 2.1,
    //                timezone = "UTC +2.0",
    //                coordinates = "37.9838° N, 23.7275° E",
    //                unemployement = 6.2
    //            };
    //            Record2.Children.Add(Child5);
    //            Record2.Children.Add(Child6);
    //            Record2.Children.Add(Child7);
    //            BusinessObjectCollection.Add(Record1);
    //            BusinessObjectCollection.Add(Record2);

    //        }
    //        return BusinessObjectCollection;
    //    }
    //}

}