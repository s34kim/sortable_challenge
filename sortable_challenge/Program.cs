using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Extensions;
using System.Web.Script.Serialization;
using System.IO;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace sortable_challenge
{
    /*
     *  Creator: Samuel Kim
     *  Date: 10JAN2013
     *  Edits:
     *   - 
     *  
     *  Written in .NET 4.5. Not too sure if this works in .NET 3.5, but the web
     *  extensions .dll came from the 3.5 collection.
     *  
     *  Load all of listings.txt into an array because we keep traversing it
     *  
     *  We read each line of the input product.txt
     *      look for the matches (more on this after)
     *      populate the next element in results with all the matches
     *  
     *  What determines a match?
     *  Go with a point system, certain matching statements increases the points
     *  and if the point total exceeds the cutoff then there is a match
     *  Using points also lets us easily adjust each match's important, introduce
     *  new match heuristics, and change the cutoffs.
     *  The current data set is pretty naive, since the manufacturer strings are nicely
     *  populated (e.g. easy to match "Canon" and "Canon Canada", etc.)
     *  
     *  Because of the ambiguity of the model (e.g. just a number) we want to consider
     *  what's a good match.
     *  If the model is a combination of letters and numbers, and matches: 1.0 points
     *  If the above doesn't match then if there is white space (this includes dashes)
     *  remove it and try again: 1.0 points
     *  If the above doesn't match, and if the letters and numbers are grouped up
     *  then try to match them separately + whitespace: 1.0 points
     *  If the model is just letters, only match if present in title surrounded with
     *  white space eg " DX ": 0.9 points
     *  If the model is just letters, only match if present in title surrounded with
     *  white space eg " 100 ": 0.9 points
     *  family string in title string: 0.2 points
     *  Manufacturer strings matching: 0.1 points
     *  I realized that every listing has manufacturer nicely listed, which makes this easy
     *  to match just based on that and not have false positives due to matching with
     *  accessory manufacturers.
     *  
     *  If >1.0 points then we assume a match
     */

    [Serializable]
    public class product
    {
        public string product_name;
        public string manufacturer;
        public string family;
        public string model;
        public string announced_date;
    }

    [Serializable]
    public class listing
    {
        public string title;
        public string manufacturer;
        public string currency;
        public string price;
    }

    [Serializable]
    public class result
    {
        public string product_name;
        public listing[] listings;
    }
    
    class Program
    {
        static void Main(string[] args)
        {
            double threshold = 1.0;

            // Let's see how long the program takes!
            Stopwatch timer = new Stopwatch();
            timer.Start();
            
            // if no args assume input files are products.txt and listings.txt
            // if args can either be help (if invalid) or the products and then listings txt files
            // eg run sort products2.txt listings2.txt

            string products_file_name = "products.txt";
            string listings_file_name = "listings.txt";
            string results_file_name = "results.txt";

            // if given file names, check if they exist
            if (args.Length == 2)
            {
                products_file_name = args[0];
                listings_file_name = args[1];
            }
            // output help message
            else if (args.Length != 0)
            {
                Console.WriteLine(".exe [optional:products file path] [optional:listings file path]");
                Console.WriteLine("If specifying the products and listings file paths, must give both.");
                Console.WriteLine("Otherwise they are assumed to be local products.txt and listings.txt");

                return;
            }
            // If files don't exist, exit
            if (!File.Exists(products_file_name))
            {
                Console.WriteLine("Products file not found!");
                return;
            }
            if (!File.Exists(listings_file_name))
            {
                Console.WriteLine("Listings file not found!");
                return;
            }
            
            // Populate the listings list (of listing objects)
            // since we transverse it for every product object in products.txt
            // it's easier to just store it in the memory, otherwise could
            // just put it in a database
            string line;
            int linecounter = 0;
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            List<listing> listings = new List<listing>();
            StreamReader listings_file = new StreamReader(listings_file_name);
            while ((line = listings_file.ReadLine()) != null)
            {
                try
                {
                    listings.Add(serializer.Deserialize<listing>(line));
                }
                catch
                {
                    Console.WriteLine("Could not read from line # " + linecounter.ToString());
                }
                ++linecounter;
            }

            // Loop through the products and generate the results
            List<result> results = new List<result>();
            StreamReader products_file = new StreamReader(products_file_name);
            product current_product = new product();
            linecounter = 0;
            Regex containsletters = new Regex(@"[a-zA-Z]+?");
            Regex containsnumbers = new Regex(@"[0-9]+?");
            Regex isaword = new Regex(@"^[a-zA-Z]+$");
            Regex isanumber = new Regex(@"^[0-9]+$");
            while ((line = products_file.ReadLine()) != null)
            {
                bool readsuccess = true;
                try
                {
                    current_product = serializer.Deserialize<product>(line);
                }
                catch
                {
                    Console.WriteLine("Could not read from line # " + linecounter.ToString());
                    readsuccess = false;
                }

                if (readsuccess)
                {
                    // now we look through all the listings for matches
                    // each match is stored in the listing
                    results.Add(new result());
                    results[linecounter].product_name = current_product.product_name;
                    List<listing> templist = new List<listing>();
                    foreach (listing current_listing in listings)
                    {
                        double points = 0;
                        // our product manufacturer fields are always nice one word entries
                        // but if this changes we can just upgrade to a smart match algorithm
                        // convert both to lowercase because .contains is case sensitive
                        // added a check if listing manufacturer field is empty
                        // then we search for it in the title
                        if (current_listing.manufacturer.ToLower().Contains(current_product.manufacturer.ToLower()))
                            points = points + 0.1;
                        else if (current_listing.title.ToLower().Contains(current_product.manufacturer.ToLower()))
                            points = points + 0.1;
                        
                        // now check if the listing title contains the product family 
                        if (current_product.family != null)
                        {
                            if (current_listing.title.ToLower().Contains(current_product.family.ToLower()))
                                points = points + 0.2;
                        }

                        // finally check if the listing title contains the model
                        // first we check if the model is a mixture of letters and numbers, 
                        // which makes a strong match easy
                        if (containsletters.IsMatch(current_product.model) && containsnumbers.IsMatch(current_product.model))
                        {
                            // This used to be a fully regex expression looking for spaces, but this took WAY
                            // too long so I switched it back to a *.Contains(*)
                            //if (Regex.IsMatch(current_listing.title.ToLower(), current_product.model.ToLower()))
                            if (current_listing.title.ToLower().Contains(current_product.model.ToLower()))
                            {
                                points = points + 1.0;
                            }
                            else
                            {
                                // remove whitespace, etc and search again
                                string tempmodel = Regex.Replace(current_product.model.ToLower(), @"[_\s-]", "");
                                if (Regex.IsMatch(current_listing.title.ToLower(), @"[\s_-]" + tempmodel + @"[$\s_-]"))
                                    points = points + 1.0;
                                else
                                {
                                    // try to split the string up into its components
                                    // this is naive in that it assumes the letters and numbers
                                    // will be grouped up (e.g. D7000 but won't work with T3i
                                    // BUT things like T3i should be handled with the previous 
                                    // match attempts)
                                    string tempmodelnumbers = Regex.Replace(current_product.model.ToLower(), @"[a-zA-Z_\s-]", "");
                                    string tempmodelletters = Regex.Replace(current_product.model.ToLower(), @"[0-9_\s-]", "");

                                    if (Regex.IsMatch(current_listing.title.ToLower(), @"[\s_-]" + tempmodelnumbers + @"[$\s_-]") &&
                                        Regex.IsMatch(current_listing.title.ToLower(), @"[\s_-]" + tempmodelletters + @"[$\s_-]"))
                                        points = points + 1.0;
                                }
                            }
                        }
                        // only letters so we want it to match with whitespace around the model
                        // less points, so we also need a family match
                        else if (isaword.IsMatch(current_product.model))
                        {
                            if (Regex.IsMatch(current_listing.title.ToLower(), @"[\s_-]" + current_product.model + @"[$\s_-]"))
                                points = points + 0.8;
                        }
                            // only numbers, but same as above
                        else if (isanumber.IsMatch(current_product.model))
                        {
                            if (Regex.IsMatch(current_listing.title.ToLower(), @"[\s_-]" + current_product.model + @"[$\s_-]"))
                                points = points + 0.8;
                        }
                        else
                        {
                            // Any left over cases will just be handled with a generic search
                            if (current_listing.title.ToLower().Contains(current_product.model.ToLower()))
                                points = points + 1.0;
                        }

                        // checks if point total gives us a positive match
                        if (points > threshold)
                        {
                            templist.Add(current_listing);
                        }

                    }//foreach

                    // put the templist List into the result Listing array
                    results[linecounter].listings = templist.ToArray();
                }//if readsuccess
                ++linecounter;
            }
            
            // Now we generate the results.txt
            StreamWriter outfile = new StreamWriter(results_file_name);
            foreach (result current_result in results)
            {
                outfile.WriteLine(serializer.Serialize(current_result));
                outfile.Flush();
            }

            // tell us the run time (6mins on my computer)
            timer.Stop();
            Console.WriteLine("Completed in " + timer.Elapsed + "!");
        }
    }
}
