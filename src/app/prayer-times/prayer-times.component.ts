import {NgModule,Component, AfterViewInit, ViewChild, ViewContainerRef,ChangeDetectorRef} from '@angular/core'
import {NgZone} from '@angular/core'
import {PrayerTimesCalculatorService, prayerTime, prayerTimesForDay, timeZoneInfo, hijriDate} from '../prayer-times-calculator.service';
import {BrowserModule} from '@angular/platform-browser';
import {RouterModule} from '@angular/router'
import {AlertComponent, DatepickerModule, ModalModule} from 'ng2-bootstrap/ng2-bootstrap';
import {NgbModule,NgbDateStruct} from '@ng-bootstrap/ng-bootstrap'
import 'moment';
import 'moment-timezone'
declare var $: any;
declare var google: any;
declare var moment: any;
declare var jsPDF:any;
@Component({
//  moduleId: module.id,
	templateUrl: './prayer-times.component.html',
  styleUrls: ['./prayer-times.component.css'],
  selector: 'prayer-times',
 // directives: [DATEPICKER_DIRECTIVES, MODAL_DIRECTVES, ROUTER_DIRECTIVES],
  providers: [PrayerTimesCalculatorService],
 // viewProviders:[BS_VIEW_PROVIDERS],
}) export class PrayerTimesComponent implements AfterViewInit {
	date: NgbDateStruct;
	latitude: number;
	longitude: number;
	initialiseMap: any;
	qiblaLine: any;
	fajrAngle: number;
	ishaAngle: number;
	timeZone: string;
	startOfLunarMonth: boolean;
	fajrIsAdjusted: boolean;
	fajrIsAdjustedEarlier: boolean;
	maghribIsAdjustedLater: boolean;
	maghribIsAdjusted: boolean;
	locationFound: string;
	searchLocation: string;
	locationNotFound: boolean=false;
	showNewMonthLegend: boolean;
	map:any;
	maxZoomService:any;
	constructor(private prayerTimesCalculatorService: PrayerTimesCalculatorService,
		private ngZone: NgZone,private changeDetectorRef:ChangeDetectorRef) {
		if(!moment){return;}
		var now=new Date();
		this.date={year: now.getFullYear(), month: now.getMonth(), day: now.getDate()}
		this.latitude = 53.482863;
		this.longitude = -2.3459968;
		//this.utcOffset=moment().utcOffset()/60.0;
		this.fajrAngle = 6;
		this.ishaAngle = 6;
	}
	resetMap(){
		var self=this;

		var mapProp = {
            center: new google.maps.LatLng(this.latitude, this.longitude),
            zoom: 10,
            mapTypeId: google.maps.MapTypeId.ROADMAP
        };
        // console.log("initialising map...");
        this.map = new google.maps.Map(document.getElementById("googleMap"), mapProp);
		this.map.addListener('click', function(e){
			// console.log("clicked "+ JSON.stringify(e) );
			self.mapClicked(e);
		});
	}
	ngAfterViewInit() {
		var self=this;
		if(google){
			this.resetMap();
			this.resetLocation();
			this.maxZoomService=new google.maps.MaxZoomService();

		}
		this.changeDetectorRef.detectChanges();
		console.log("the date is"+this.getFullDate());
	}
	getWeekDay() {
		if(!moment){return "";}
		return moment(this.date).format("dddd");
	}
	getFullDate() {
		if(!moment){return "";}
		return moment(this.date).format("D MMMM YYYY");
	}
	removeCalendar(){
		this.numberOfDaysInCalendar = null;
		this.buildCalendar();
		this.changeDetectorRef.detectChanges();
		this.resetMap();
		this.placeQiblaOnMap();		
	}
	zoomToBuilding(){
		var self=this;
		var latlng = { lat: self.latitude, lng: self.longitude };
		this.maxZoomService.getMaxZoomAtLatLng(latlng,(response)=>{
			if(response.status !== 'OK'){
				alert("Sorry, google maps returned an error");
			}else{
				self.map.setZoom(response.zoom);
				self.map.setMapTypeId('hybrid');
			}
		})
	}
	resetLocation(){
		//console.log("resetting location on map"+this.map);
		var self = this;
		self.map.setMapTypeId('roadmap');

		if (navigator.geolocation) {
			navigator.geolocation.getCurrentPosition(function(p) {
				self.latitude = p.coords.latitude;
				self.longitude = p.coords.longitude;
				self.placeQiblaOnMap();
				self.getPrayerTimes();
				self.buildCalendar();
			},function(e){
				self.placeQiblaOnMap();
				self.getPrayerTimes();
				self.buildCalendar();

				console.log("could not get your current location:"+e.message)
			});
		}
		self.map.setZoom(10);
	}
	searchForLocation(){
		var self = this;
		//console.log("searching location on map"+this.map);

		self.locationNotFound = false;
		if (self.searchLocation != null && self.searchLocation != '') {
			var geocoder = new google.maps.Geocoder;
			var infowindow = new google.maps.InfoWindow;
			var latlng = { lat: self.latitude, lng: self.longitude };
			geocoder.geocode({ 'address': self.searchLocation }, function(results, status) {
				if (status === google.maps.GeocoderStatus.OK) {
					if (results[0]) {
						self.locationFound = results[0].formatted_address;
						self.latitude = results[0].geometry.location.lat();
						self.longitude = results[0].geometry.location.lng();
						self.placeQiblaOnMap();
						self.getPrayerTimes();
						self.buildCalendar();
						self.map.setZoom(10);
					}
				}
				if (status === google.maps.GeocoderStatus.ZERO_RESULTS){
					self.locationNotFound = true;
				}
				self.ngZone.run(function() { });
			});
		};
	}
	getLocationFound(){
		var self = this;
		var geocoder = new google.maps.Geocoder;
		var infowindow = new google.maps.InfoWindow;
		var latlng = { lat: self.latitude, lng: self.longitude };
		geocoder.geocode({ 'location': latlng }, function(results, status) {
			if (status === google.maps.GeocoderStatus.OK) {
				if(results[0]){
					self.locationFound = results[0].formatted_address;
					self.ngZone.run(function(){});
				} 
			}
		});
	}
	placeQiblaOnMap() {
		var self = this;
		// console.log("placing qibla on map"+this.map);
		if(!this.map){return;}
		var qiblaLineCoords = [
			{ lat: this.latitude, lng: this.longitude },
			{ lat: 21.422441833015604, lng: 39.82616901397705 }
		];
		if (this.qiblaLine != null) {
			this.qiblaLine.setMap(null);
		}
		self.qiblaLine = new google.maps.Polyline({
			path: qiblaLineCoords,
			geodesic: true,
			strokeColor: '#FF0000',
			strokeOpacity: 1.0,
			strokeWeight: 2
		});
		self.qiblaLine.setMap(this.map);
		this.map.setCenter({ lat: this.latitude, lng: this.longitude });

		self.getLocationFound();
	}
	prayerTimes: prayerTime[]=[]
	calendar: prayerTimesForDay[] = [];
	numberOfDaysInCalendar:number
	buildingCalendar: boolean = false;
	buildCalendar(){
		this.getPrayerTimeTableForNextNDays(this.numberOfDaysInCalendar);
	}
	getDate(){
		return new Date(this.date.year,this.date.month,this.date.day);
	}
	getPrayerTimeTableForTimeZone(days:number,initialTimeZone:timeZoneInfo){
		var self = this;

		self.calendar = [];
		var dateMoment = moment(self.date).startOf('d');
		var yesterdayHijriDate=null;
		var yesterdayWasNewMoon=false;
		var previousTimeZoneAbbreviation=null;
		for (var i = 0; i < days; i++) {
			var date = moment(dateMoment).add(i, 'd');
			var prayerTimesForDate= self.getPrayerTimesForDate(date, initialTimeZone, yesterdayHijriDate,yesterdayWasNewMoon);
			if(previousTimeZoneAbbreviation && prayerTimesForDate.timeZoneAbbreviation!=previousTimeZoneAbbreviation )
			{
				prayerTimesForDate.timeZoneChange=true;
			}
			self.calendar.push(prayerTimesForDate);
			yesterdayHijriDate = prayerTimesForDate.hijriDate;
			yesterdayWasNewMoon = prayerTimesForDate.startOfLunarMonth;
			previousTimeZoneAbbreviation=prayerTimesForDate.timeZoneAbbreviation;
		}
		var firstDay = self.calendar[0];
		self.showNewMonthLegend = self.calendar.some(function(day){
			return day.startOfLunarMonth;
		})

	}
// 	(function(API){
//     API.textAlign = function(txt, options, x, y) {
//         options = options ||{};
//         // Use the options align property to specify desired text alignment
//         // Param x will be ignored if desired text alignment is 'center'.
//         // Usage of options can easily extend the function to apply different text
//         // styles and sizes

// 		// Get current font size
//         var fontSize = this.internal.getFontSize();

//         // Get page width
//         var pageWidth = this.internal.pageSize.width;

//         // Get the actual text's width
//         // You multiply the unit width of your string by your font size and divide
//         // by the internal scale factor. The division is necessary
//         // for the case where you use units other than 'pt' in the constructor
//         // of jsPDF.

//         var txtWidth = this.getStringUnitWidth(txt)*fontSize/this.internal.scaleFactor;

//         if( options.align === "center" ){

//             // Calculate text's x coordinate
//             x = ( pageWidth - txtWidth ) / 2;

//         } else if( options.align === "centerAtX" ){ // center on X value

// 	        x = x - (txtWidth/2);

//         } else if(options.align === "right") {

// 	        x = x - txtWidth;
//         }

//         // Draw text at x,y
//         this.text(txt,x,y);
//     };
// /*
//     API.textWidth = function(txt) {
// 	    var fontSize = this.internal.getFontSize();
//         return this.getStringUnitWidth(txt)*fontSize / this.internal.scaleFactor;
//     };
// */

//     API.getLineHeight = function(txt) {
//         return this.internal.getLineHeight();
//     };

// })(jsPDF.API);
	getTextWidth(pdf:any,text:string):number{
		var txtWidth = pdf.getStringUnitWidth(text)*pdf.internal.getFontSize()/pdf.internal.scaleFactor;
		return txtWidth
	}
	placeTextCenteredOnPdf(pdf:any,text:string,xPosition:number,yPosition:number){
		pdf.text(text, xPosition-(this.getTextWidth(pdf,text)/2),yPosition );
	}
	topPosMm:number=30;
	pageNumber:number=1;
	addPdfHeader(pdf:any){
		pdf.addImage(this.logoSrc, 'JPEG', 20, 15, 20, 20);
		this.topPosMm=30;
		pdf.setFontSize(20);
		this.placeTextCenteredOnPdf(pdf,'ShariahStandards.org Prayer Timetable', 105, this.topPosMm);
		//pdf.text(30, 30, 'ShariahStandards.org Prayer Timetable');
		this.topPosMm+=10;
		pdf.setFontSize(15);
		this.placeTextCenteredOnPdf(pdf,this.locationFound, 105, this.topPosMm);
		this.topPosMm+=20;
		pdf.setFontSize(12);
		
		pdf.text("Date", 20,this.topPosMm);
		pdf.text("Hijri", 50,this.topPosMm);
		pdf.text("Fajr", 80,this.topPosMm);
		pdf.text("Sunrise", 100,this.topPosMm);
		pdf.text("Zuhr", 120,this.topPosMm);
		pdf.text("Asr", 140,this.topPosMm);
		pdf.text("Maghrib", 160,this.topPosMm);
		pdf.text("Isha", 180,this.topPosMm);
		this.topPosMm+=8;
		pdf.setFontSize(10);
	}
	logoSrc:string
	="data:image/jpeg;base64,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"
	//="data:image/png;base64,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"
	getPdfTimetable(){
		if(!this.locationFound){return;}
		if(!this.calendar){return;}
		if(this.calendar.length==0){return;}
		this.pageNumber=1;
		var pdf = new jsPDF('p', 'mm', 'a4');
		this.addPdfHeader(pdf);
		var self=this;
		this.calendar.forEach(day=>{
			if(self.topPosMm>275){
				pdf.addPage();
				self.addPdfHeader(pdf);
			}
			if(day.timeZoneChange){
				this.placeTextCenteredOnPdf(pdf,"*** Start of "+day.timeZoneAbbreviation+" ***", 105, self.topPosMm);
				self.topPosMm+=6;
				pdf.setFontType("bold");
			}
			if(!day.timeZoneChange){
				pdf.setFontType("normal");
			}
			if(day.isFriday){
				pdf.setFillColor(180,200,180);
				pdf.roundedRect(19.5,self.topPosMm-4, 170, 6, 0, 0, "F");
			}
			pdf.text(day.weekDay+" "+day.simpleDate, 20,self.topPosMm);
			pdf.text(day.hijriDate.day+" "+day.hijriDate.month.englishName, 50,self.topPosMm);
			pdf.text(day.times["fajr"], 80,self.topPosMm);
			pdf.text(day.times["sunrise"], 100,self.topPosMm);
			pdf.text(day.times["zuhr"], 120,self.topPosMm);
			pdf.text(day.times["asr"], 140,self.topPosMm);
			pdf.text(day.times["maghrib"], 160,self.topPosMm);
			pdf.text(day.times["isha"], 180,self.topPosMm);
			self.topPosMm+=6;
			
		})

	
        pdf.save('ShariahStandardsTimeTable'+this.latitude+"_"+this.longitude+'_'+moment(this.date).format("YYYYMMDD")+this.numberOfDaysInCalendar+'.pdf');	
	}
	getPrayerTimeTableForNextNDays(days:number){
		var self = this;
		self.showNewMonthLegend = false;
		self.calendar = [];
		if(days==null){
			return;
		}
		
		return self.prayerTimesCalculatorService.getDefaultTimeZone(self.getDate(), self.latitude, self.longitude)
			.subscribe(initialTimeZone => {

				// var lastDate=moment(self.getDate()).add(days,"days").toDate();
				// self.prayerTimesCalculatorService.getDefaultTimeZone(lastDate,self.latitude,self.longitude)
				// .subscribe(finalTimeZone=>{
				// 	// if(initialTimeZone.dstOffset==finalTimeZone.dstOffset
					// && finalTimeZone.rawOffset== initialTimeZone.rawOffset)
					// {
						self.getPrayerTimeTableForTimeZone(days,initialTimeZone)
					// }else{

					// }
				//})
			});
	}
	hijriDate:hijriDate;
	getPrayerTimes() {
		var self = this;
		return self.prayerTimesCalculatorService.getDefaultTimeZone(self.getDate(), self.latitude, self.longitude)
			.subscribe(timeZone => {
				console.log("getting prayer times");
				var prayerTimesDay = self.getPrayerTimesForDate(self.getDate(), timeZone, null,false);
				self.prayerTimes = prayerTimesDay.times;
				self.startOfLunarMonth = prayerTimesDay.startOfLunarMonth;
				self.timeZone = prayerTimesDay.timeZoneAbbreviation;
				self.fajrIsAdjusted = prayerTimesDay.fajrIsAdjusted;
				self.maghribIsAdjusted = prayerTimesDay.maghribIsAdjusted;
				self.fajrIsAdjustedEarlier = prayerTimesDay.fajrIsAdjustedEarlier;
				self.maghribIsAdjustedLater = prayerTimesDay.maghribIsAdjustedLater;
				self.hijriDate=prayerTimesDay.hijriDate;
				console.log(self.hijriDate);
			});
	}
	getPrayerTimesForDate(date:Date,timeZone:timeZoneInfo,yesterdayHijri?:hijriDate,yesterdayWasNewMoon?:boolean) {
		var self = this;
		return self.prayerTimesCalculatorService.getPrayerTimes(date,
			self.latitude, self.longitude, timeZone, yesterdayHijri,yesterdayWasNewMoon);
	}
	dateChanged(){
		console.log("the date is"+this.getFullDate());
		this.getPrayerTimes();
		this.getPrayerTimeTableForNextNDays(this.numberOfDaysInCalendar);
	}
	mapClicked($event: any) {
		// console.log($event);
		this.locationNotFound = false;
		this.latitude = $event.latLng.lat();
		this.longitude = $event.latLng.lng();
		this.placeQiblaOnMap();
		this.getPrayerTimes();
		this.getPrayerTimeTableForNextNDays(this.numberOfDaysInCalendar);
    }
}
