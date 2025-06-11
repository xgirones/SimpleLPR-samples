#!/usr/bin/env python3
"""
SimpleLPR Python Tracking Demo

A comprehensive demonstration of SimpleLPR's license plate tracking functionality.
Processes video sources (files or streams) and tracks license plates across frames,
saving results as JSON files with thumbnails.

This demo showcases:
- Video processing with license plate detection
- Multi-frame tracking to group detections of the same plate
- Asynchronous processing using processor pools
- Graceful shutdown handling
- Progress reporting
- Result persistence in JSON format

Requirements:
- Python 3.8+
- simplelpr package (pip install simplelpr)
- Valid license key for production use (60-day evaluation available)

Usage:
    python simplelpr_tracking_demo.py <video_source> <country_id> <output_folder> [product_key]

Example:
    python simplelpr_tracking_demo.py traffic.mp4 Spain ./results license.xml

Copyright: (c) Warelogic
License: See SimpleLPR licensing terms
"""

import argparse
import json
import logging
import os
import signal
import sys
import time
from collections import deque
from dataclasses import dataclass, asdict
from datetime import datetime
from pathlib import Path
from typing import Optional, List, Dict, Any, Deque

import simplelpr


# Global flag for graceful shutdown
_shutdown_requested = False


def signal_handler(signum, frame):
    """Handle interrupt signals for graceful shutdown."""
    global _shutdown_requested
    _shutdown_requested = True
    print("\n\nShutdown requested. Finishing current operations...")


@dataclass
class TrackingConfig:
    """Configuration for the tracking system."""
    video_source: str
    country_id: str
    output_folder: Path
    product_key: Optional[str] = None
    
    # Tracking parameters
    trigger_window_sec: float = 3.0
    max_idle_time_sec: float = 2.0
    min_trigger_frame_count: int = 3
    thumbnail_width: int = 256
    thumbnail_height: int = 128
    
    # Processing parameters
    max_concurrent_ops: int = 0  # 0 = auto-detect
    plate_region_detection: bool = True
    crop_to_plate_region: bool = False
    
    # Video parameters
    max_video_width: int = 1920
    max_video_height: int = 1080


@dataclass
class TrackResult:
    """Represents a tracked license plate with metadata."""
    track_id: int
    plate_text: str
    country: str
    country_iso: str
    confidence: float
    
    # Timing information
    first_frame_id: int
    first_timestamp: float
    last_frame_id: int
    last_timestamp: float
    representative_frame_id: int
    representative_timestamp: float
    
    # Detection statistics
    duration_seconds: float
    total_frames: int
    
    # File paths
    thumbnail_path: Optional[str] = None
    json_path: Optional[str] = None
    
    # Detailed match data
    match_details: Optional[Dict[str, Any]] = None


class SimpleLPRTracker:
    """Main class for SimpleLPR video tracking functionality."""
    
    def __init__(self, config: TrackingConfig):
        """Initialize the tracker with given configuration."""
        self.config = config
        self.logger = self._setup_logging()
        
        # SimpleLPR objects
        self.engine: Optional[simplelpr.SimpleLPR] = None
        self.processor_pool: Optional[simplelpr.ProcessorPool] = None
        self.tracker: Optional[simplelpr.PlateCandidateTracker] = None
        self.video_source: Optional[simplelpr.VideoSource] = None
        
        # Tracking state
        self.track_counter = 0
        self.frame_count = 0
        self.start_time = time.time()
        self.tracks: List[TrackResult] = []
        
    def _setup_logging(self) -> logging.Logger:
        """Configure logging for the application."""
        log_format = '%(asctime)s - %(name)s - %(levelname)s - %(message)s'
        logging.basicConfig(
            level=logging.INFO,
            format=log_format,
            handlers=[
                logging.StreamHandler(sys.stdout),
                logging.FileHandler(self.config.output_folder / 'tracking.log')
            ]
        )
        return logging.getLogger('SimpleLPRTracker')
    
    def initialize_engine(self) -> None:
        """Initialize SimpleLPR engine with configuration."""
        self.logger.info("Initializing SimpleLPR engine...")
        
        # Setup engine parameters
        setup_params = simplelpr.EngineSetupParms()
        setup_params.cudaDeviceId = -1  # CPU mode
        setup_params.enableImageProcessingWithGPU = False
        setup_params.enableClassificationWithGPU = False
        setup_params.maxConcurrentImageProcessingOps = self.config.max_concurrent_ops
        
        # Create engine
        self.engine = simplelpr.SimpleLPR(setup_params)
        
        # Log version
        version = self.engine.versionNumber
        self.logger.info(f"SimpleLPR version: {version.A}.{version.B}.{version.C}.{version.D}")
        
        # Apply product key if provided
        if self.config.product_key:
            if not os.path.exists(self.config.product_key):
                raise FileNotFoundError(f"Product key file not found: {self.config.product_key}")
            self.engine.set_productKey(self.config.product_key)
            self.logger.info("Product key loaded successfully")
        else:
            self.logger.info("Running in evaluation mode")
        
        # Configure country weights
        self._configure_countries()
        
        # Create processor pool
        self.processor_pool = self.engine.createProcessorPool(
            setup_params.maxConcurrentImageProcessingOps
        )
        self.processor_pool.plateRegionDetectionEnabled = self.config.plate_region_detection
        self.processor_pool.cropToPlateRegionEnabled = self.config.crop_to_plate_region
        
        # Create tracker
        tracker_params = simplelpr.PlateCandidateTrackerSetupParms(
            triggerWindowInSec=self.config.trigger_window_sec,
            maxIdleTimeInSec=self.config.max_idle_time_sec,
            minTriggerFrameCount=self.config.min_trigger_frame_count,
            thumbnailWidth=self.config.thumbnail_width,
            thumbnailHeight=self.config.thumbnail_height
        )
        self.tracker = self.engine.createPlateCandidateTracker(tracker_params)
        
        self.logger.info("Engine initialization complete")
    
    def _configure_countries(self) -> None:
        """Configure country weights for license plate recognition."""
        # First, disable all countries
        for i in range(self.engine.numSupportedCountries):
            self.engine.set_countryWeight(i, 0.0)
        
        # Try to set by string ID first
        try:
            self.engine.set_countryWeight(self.config.country_id, 1.0)
            self.logger.info(f"Country configured: {self.config.country_id}")
        except:
            # If string ID fails, try as integer
            try:
                country_idx = int(self.config.country_id)
                if 0 <= country_idx < self.engine.numSupportedCountries:
                    self.engine.set_countryWeight(country_idx, 1.0)
                    country_name = self.engine.get_countryCode(country_idx)
                    self.logger.info(f"Country configured: {country_name} (index {country_idx})")
                else:
                    raise ValueError(f"Country index {country_idx} out of range")
            except ValueError:
                # List available countries and exit
                self._list_countries()
                raise ValueError(f"Invalid country identifier: {self.config.country_id}")
        
        # Apply country weights
        self.engine.realizeCountryWeights()
    
    def _list_countries(self) -> None:
        """List all supported countries."""
        print("\nSupported countries:")
        for i in range(self.engine.numSupportedCountries):
            print(f"  {i:3d} : {self.engine.get_countryCode(i)}")
        print()
    
    def open_video_source(self) -> None:
        """Open the video source for processing."""
        self.logger.info(f"Opening video source: {self.config.video_source}")
        
        self.video_source = self.engine.openVideoSource(
            self.config.video_source,
            simplelpr.FrameFormat.FRAME_FORMAT_BGR24,
            self.config.max_video_width,
            self.config.max_video_height
        )
        
        if self.video_source.state != simplelpr.VideoSourceState.VIDEO_SOURCE_STATE_OPEN:
            raise RuntimeError(f"Failed to open video source. State: {self.video_source.state}")
        
        source_type = "Live stream" if self.video_source.isLiveSource else "Video file"
        self.logger.info(f"Video source type: {source_type}")
    
    def process_video(self) -> None:
        """Main video processing loop."""
        global _shutdown_requested
        
        print("\nProcessing video... Press Ctrl+C to stop.\n")
        
        frame_queue: Deque[simplelpr.VideoFrame] = deque()
        last_progress_update = time.time()
        
        try:
            while not _shutdown_requested:
                # Get next frame
                frame = self.video_source.nextFrame()
                if frame is None:
                    if self.video_source.isLiveSource:
                        # For live sources, try to reconnect
                        self.logger.warning("No frame received from live source, attempting reconnect...")
                        self.video_source.reconnect()
                        time.sleep(0.1)
                        continue
                    else:
                        # End of file
                        break
                
                self.frame_count += 1
                frame_queue.append(frame)
                
                # Launch analysis
                success = self.processor_pool.launchAnalyze(
                    streamId=0,
                    requestId=frame.sequenceNumber,
                    timeoutInMs=simplelpr.TIMEOUT_INFINITE,
                    frame=frame
                )
                
                if not success:
                    self.logger.error(f"Failed to launch analysis for frame {frame.sequenceNumber}")
                    frame_queue.pop()
                    continue
                
                # Process completed results
                self._process_pending_results(frame_queue)
                
                # Update progress display
                if time.time() - last_progress_update > 0.5:
                    self._update_progress_display()
                    last_progress_update = time.time()
            
            # Process remaining frames
            if not _shutdown_requested:
                print("\n\nProcessing remaining frames...")
                while self.processor_pool.ongoingRequestCount_get(0) > 0:
                    result = self.processor_pool.pollNextResult(0, 100)  # 100ms timeout
                    if result:
                        self._process_result(result, frame_queue)
                
                # Flush tracker
                print("Finalizing tracking results...")
                flush_result = self.tracker.flush()
                self._process_tracker_result(flush_result, -1.0)
        
        except KeyboardInterrupt:
            # Handled by signal handler
            pass
        
        except Exception as e:
            self.logger.error(f"Error during video processing: {e}")
            raise
        
        finally:
            # Clean up remaining frames
            while frame_queue:
                frame_queue.popleft()  # Frames are automatically cleaned up in Python
    
    def _process_pending_results(self, frame_queue: Deque[simplelpr.VideoFrame]) -> None:
        """Process all pending analysis results."""
        while True:
            result = self.processor_pool.pollNextResult(0, simplelpr.TIMEOUT_IMMEDIATE)
            if result is None:
                break
            self._process_result(result, frame_queue)
    
    def _process_result(self, result: simplelpr.ProcessorPoolResult, 
                       frame_queue: Deque[simplelpr.VideoFrame]) -> None:
        """Process a single analysis result."""
        if not frame_queue:
            self.logger.warning("Result received but frame queue is empty")
            return
        
        frame = frame_queue.popleft()
        
        try:
            if result.errorInfo:
                self.logger.error(f"Error processing frame {frame.sequenceNumber}: "
                                 f"{result.errorInfo.description}")
                return
            
            if not result.candidates:
                return  # No candidates in this frame
            
            # Process with tracker
            tracker_result = self.tracker.processFrameCandidates(result, frame)
            self._process_tracker_result(tracker_result, frame.timestamp)
            
        except Exception as e:
            self.logger.error(f"Error processing result for frame {frame.sequenceNumber}: {e}")
    
    def _process_tracker_result(self, tracker_result: simplelpr.PlateCandidateTrackerResult,
                               timestamp: float) -> None:
        """Process tracker results for new and closed tracks."""
        # Process new tracks (just for logging)
        for track in tracker_result.newTracks:
            candidate = track.representativeCandidate
            if candidate.matches:
                match = candidate.matches[0]
                is_matched = bool(match.countryISO)
                
                # Clear progress line before printing
                print('\r' + ' ' * 80 + '\r', end='')
                
                print(f"[NEW] Frame {track.firstDetectionFrameId:6d} @ "
                      f"{track.firstDetectionTimestamp:6.2f}s: "
                      f"{match.text:<12} "
                      f"{'(' + match.country + ')' if is_matched else '(unmatched)'}")
        
        # Process closed tracks (save data)
        for track in tracker_result.closedTracks:
            self.track_counter += 1
            self._save_track_data(track)
    
    def _save_track_data(self, track: simplelpr.TrackedPlateCandidate) -> None:
        """Save track data to JSON and thumbnail image."""
        candidate = track.representativeCandidate
        if not candidate.matches:
            self.logger.warning("Track with no matches encountered")
            return
        
        best_match = candidate.matches[0]
        is_matched = bool(best_match.countryISO)
        
        # Calculate statistics
        duration = track.newestDetectionTimestamp - track.firstDetectionTimestamp
        frame_range = track.newestDetectionFrameId - track.firstDetectionFrameId + 1
        
        # Clear progress line before printing
        print('\r' + ' ' * 80 + '\r', end='')
        
        print(f"[CLOSED] {best_match.text:<12} "
              f"{'(' + best_match.country + ')' if is_matched else '(unmatched)':<15} "
              f"Duration: {duration:4.2f}s, Frames: {frame_range:3d} "
              f"[{track.firstDetectionFrameId}-{track.newestDetectionFrameId}]")
        
        # Create safe filename
        safe_text = self._sanitize_filename(best_match.text)
        base_filename = f"track_{track.representativeFrameId:06d}_{track.representativeTimestamp:.2f}_{safe_text}"
        
        # Save thumbnail if available
        thumbnail_path = None
        if track.representativeThumbnail:
            thumbnail_path = self.config.output_folder / f"{base_filename}.jpg"
            track.representativeThumbnail.saveAsJPEG(str(thumbnail_path), 95)
        
        # Prepare track result data
        track_result = TrackResult(
            track_id=self.track_counter,
            plate_text=best_match.text,
            country=best_match.country or "",
            country_iso=best_match.countryISO or "",
            confidence=best_match.confidence,
            first_frame_id=track.firstDetectionFrameId,
            first_timestamp=track.firstDetectionTimestamp,
            last_frame_id=track.newestDetectionFrameId,
            last_timestamp=track.newestDetectionTimestamp,
            representative_frame_id=track.representativeFrameId,
            representative_timestamp=track.representativeTimestamp,
            duration_seconds=duration,
            total_frames=frame_range,
            thumbnail_path=thumbnail_path.name if thumbnail_path else None,
            match_details=self._extract_match_details(candidate)
        )
        
        # Save JSON data
        json_path = self.config.output_folder / f"{base_filename}.json"
        track_result.json_path = json_path.name
        self._save_track_json(track_result, json_path)
        
        # Store in tracks list
        self.tracks.append(track_result)
    
    def _extract_match_details(self, candidate: simplelpr.Candidate) -> Dict[str, Any]:
        """Extract detailed match information from a candidate."""
        return {
            "brightBackground": candidate.darkOnLight,
            "plateDetectionConfidence": candidate.plateDetectionConfidence,
            "boundingBox": {
                "left": candidate.boundingBox.left,
                "top": candidate.boundingBox.top,
                "width": candidate.boundingBox.width,
                "height": candidate.boundingBox.height
            },
            "plateRegionVertices": [
                {"x": v.x, "y": v.y} for v in candidate.plateRegionVertices
            ] if candidate.plateRegionVertices else [],
            "matches": [
                {
                    "text": match.text,
                    "country": match.country or "",
                    "countryISO": match.countryISO or "",
                    "confidence": match.confidence,
                    "isRawText": not bool(match.countryISO),
                    "elements": [
                        {
                            "glyph": elem.glyph,
                            "confidence": elem.confidence,
                            "boundingBox": {
                                "left": elem.boundingBox.left,
                                "top": elem.boundingBox.top,
                                "width": elem.boundingBox.width,
                                "height": elem.boundingBox.height
                            }
                        } for elem in match.elements
                    ]
                } for match in candidate.matches
            ]
        }
    
    def _save_track_json(self, track: TrackResult, json_path: Path) -> None:
        """Save track data to a JSON file."""
        # Convert dataclass to dict and format for JSON
        track_data = {
            "metadata": {
                "trackId": track.track_id,
                "firstDetectionFrameId": track.first_frame_id,
                "firstDetectionTimestamp": track.first_timestamp,
                "lastDetectionFrameId": track.last_frame_id,
                "lastDetectionTimestamp": track.last_timestamp,
                "representativeFrameId": track.representative_frame_id,
                "representativeTimestamp": track.representative_timestamp,
                "durationSeconds": track.duration_seconds,
                "totalFrames": track.total_frames,
                "thumbnailPath": track.thumbnail_path
            },
            "recognition": {
                "plateText": track.plate_text,
                "country": track.country,
                "countryISO": track.country_iso,
                "confidence": track.confidence
            },
            "candidateDetails": track.match_details
        }
        
        with open(json_path, 'w') as f:
            json.dump(track_data, f, indent=2)
    
    def _update_progress_display(self) -> None:
        """Update the progress display line."""
        elapsed = time.time() - self.start_time
        fps = self.frame_count / elapsed if elapsed > 0 else 0
        
        progress_msg = (f"\rProcessing: {self.frame_count:5d} frames, "
                       f"{self.track_counter:3d} tracks, "
                       f"{fps:5.1f} FPS")
        print(progress_msg, end='', flush=True)
    
    def _sanitize_filename(self, text: str) -> str:
        """Convert text to a safe filename."""
        # Replace invalid characters with underscores
        invalid_chars = '<>:"/\\|?*'
        sanitized = text
        for char in invalid_chars:
            sanitized = sanitized.replace(char, '_')
        
        # Limit length and handle empty strings
        sanitized = sanitized[:50]  # Reasonable length limit
        return sanitized if sanitized else "unknown"
    
    def save_summary(self) -> None:
        """Save a summary of all processed tracks."""
        summary_path = self.config.output_folder / "tracking_summary.json"
        
        elapsed = time.time() - self.start_time
        
        summary = {
            "processingInfo": {
                "videoSource": self.config.video_source,
                "country": self.config.country_id,
                "processingTime": elapsed,
                "totalFrames": self.frame_count,
                "averageFPS": self.frame_count / elapsed if elapsed > 0 else 0,
                "totalTracks": self.track_counter,
                "timestamp": datetime.now().isoformat()
            },
            "trackingParameters": {
                "triggerWindowSec": self.config.trigger_window_sec,
                "maxIdleTimeSec": self.config.max_idle_time_sec,
                "minTriggerFrameCount": self.config.min_trigger_frame_count
            },
            "tracks": [
                {
                    "trackId": track.track_id,
                    "plateText": track.plate_text,
                    "country": track.country,
                    "confidence": track.confidence,
                    "durationSeconds": track.duration_seconds,
                    "totalFrames": track.total_frames,
                    "jsonFile": track.json_path,
                    "thumbnailFile": track.thumbnail_path
                } for track in self.tracks
            ]
        }
        
        with open(summary_path, 'w') as f:
            json.dump(summary, f, indent=2)
        
        self.logger.info(f"Summary saved to: {summary_path}")
    
    def cleanup(self) -> None:
        """Clean up resources."""
        self.logger.info("Cleaning up resources...")
        # Python's garbage collector handles cleanup automatically
        # but we can explicitly set to None to be clear
        self.tracker = None
        self.processor_pool = None
        self.video_source = None
        self.engine = None
    
    def run(self) -> None:
        """Main execution method."""
        try:
            # Ensure output directory exists
            self.config.output_folder.mkdir(parents=True, exist_ok=True)
            
            # Initialize engine
            self.initialize_engine()
            
            # Open video source
            self.open_video_source()
            
            # Process video
            self.process_video()
            
            # Save summary
            self.save_summary()
            
            # Print final statistics
            self._print_final_stats()
            
        except Exception as e:
            self.logger.error(f"Fatal error: {e}")
            raise
        
        finally:
            self.cleanup()
    
    def _print_final_stats(self) -> None:
        """Print final processing statistics."""
        elapsed = time.time() - self.start_time
        
        print("\n" + "="*60)
        print("Processing Complete!")
        print("="*60)
        print(f"Total frames processed: {self.frame_count:,}")
        print(f"Total tracks detected:  {self.track_counter:,}")
        print(f"Processing time:        {elapsed:.1f} seconds")
        print(f"Average FPS:           {self.frame_count/elapsed:.1f}")
        print(f"Results saved to:      {self.config.output_folder}")
        print("="*60)


def list_supported_countries():
    """List all countries supported by SimpleLPR."""
    print("\nInitializing SimpleLPR to list supported countries...\n")
    
    setup_params = simplelpr.EngineSetupParms()
    setup_params.cudaDeviceId = -1
    
    engine = simplelpr.SimpleLPR(setup_params)
    
    # Show version info
    version = engine.versionNumber
    print(f"SimpleLPR version: {version.A}.{version.B}.{version.C}.{version.D}")
    print()
    
    print("Supported countries:")
    print("-" * 30)
    for i in range(engine.numSupportedCountries):
        print(f"  {i:3d} : {engine.get_countryCode(i)}")
    print("-" * 30)
    print("\nYou can use either the country name or its index as the country_id parameter.")


def parse_arguments():
    """Parse command-line arguments."""
    parser = argparse.ArgumentParser(
        description="SimpleLPR License Plate Tracking Demo",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  %(prog)s traffic.mp4 Spain ./results
  %(prog)s rtsp://camera:554/stream Germany ./output license.xml
  %(prog)s video.avi 14 ./tracks

To see the list of supported countries:
  %(prog)s --list-countries
        """
    )
    
    parser.add_argument(
        "video_source",
        nargs='?',
        help="Path to video file or stream URL (e.g., rtsp://...)"
    )
    
    parser.add_argument(
        "country_id",
        nargs='?',
        help="Country identifier (name or index, use --list-countries to see options)"
    )
    
    parser.add_argument(
        "output_folder",
        nargs='?',
        help="Directory where results will be saved"
    )
    
    parser.add_argument(
        "product_key",
        nargs='?',
        help="Optional path to product key file (runs in evaluation mode if not provided)"
    )
    
    parser.add_argument(
        "--list-countries",
        action="store_true",
        help="List all supported countries and exit"
    )
    
    # Optional parameters for advanced users
    parser.add_argument(
        "--trigger-window",
        type=float,
        default=3.0,
        help="Trigger window in seconds (default: 3.0)"
    )
    
    parser.add_argument(
        "--max-idle-time",
        type=float,
        default=2.0,
        help="Maximum idle time in seconds before closing a track (default: 2.0)"
    )
    
    parser.add_argument(
        "--min-frames",
        type=int,
        default=3,
        help="Minimum frames required to trigger a track (default: 3)"
    )
    
    parser.add_argument(
        "--max-width",
        type=int,
        default=1920,
        help="Maximum video width for processing (default: 1920)"
    )
    
    parser.add_argument(
        "--max-height",
        type=int,
        default=1080,
        help="Maximum video height for processing (default: 1080)"
    )
    
    args = parser.parse_args()
    
    # Handle --list-countries
    if args.list_countries:
        list_supported_countries()
        sys.exit(0)
    
    # Validate required arguments
    if not all([args.video_source, args.country_id, args.output_folder]):
        parser.error("video_source, country_id, and output_folder are required unless using --list-countries")
    
    return args


def main():
    """Main entry point for the application."""
    # Parse command-line arguments
    args = parse_arguments()
    
    # Set up signal handling for graceful shutdown
    signal.signal(signal.SIGINT, signal_handler)
    signal.signal(signal.SIGTERM, signal_handler)
    
    # Create configuration
    config = TrackingConfig(
        video_source=args.video_source,
        country_id=args.country_id,
        output_folder=Path(args.output_folder),
        product_key=args.product_key,
        trigger_window_sec=args.trigger_window,
        max_idle_time_sec=args.max_idle_time,
        min_trigger_frame_count=args.min_frames,
        max_video_width=args.max_width,
        max_video_height=args.max_height
    )
    
    # Create and run tracker
    tracker = SimpleLPRTracker(config)
    
    try:
        tracker.run()
    except KeyboardInterrupt:
        # Already handled by signal handler
        pass
    except Exception as e:
        print(f"\nError: {e}")
        sys.exit(1)


if __name__ == "__main__":
    main()
